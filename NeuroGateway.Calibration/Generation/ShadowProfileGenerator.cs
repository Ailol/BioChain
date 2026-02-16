using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using NeuroGateway.AnalysisFramework;
using Spectre.Console;
using YamlDotNet.Serialization;

namespace NeuroGateway.Calibration.Generation;

public class ShadowProfileGenerator(PromptBuilder promptBuilder, IChatClient? chatClient)
{
    private static readonly string OutputDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "NeuroGateway.Calibration", "Outputs");

    private static readonly Dictionary<string, string> DimensionNameMap = BuildDimensionNameMap();

    private static Dictionary<string, string> BuildDimensionNameMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dim in DimensionDefinitions.All)
        {
            var name = dim.Name;
            map[name] = name;
            map[name.ToLowerInvariant()] = name;
            map[name.Replace(" ", "_").ToLowerInvariant()] = name;
            // Handle & variants: "Purpose & Meaning" → also match "Purpose And Meaning", "Purpose Meaning"
            if (name.Contains('&'))
            {
                map[name.Replace("&", "And")] = name;
                map[name.Replace("& ", "").Replace("  ", " ")] = name;
                map[name.Replace("&", "And").ToLowerInvariant()] = name;
                map[name.Replace("&", "And").Replace(" ", "_").ToLowerInvariant()] = name;
                map[name.Replace("& ", "").Replace("  ", " ").ToLowerInvariant()] = name;
            }
            // Handle hyphen variants: "Work-Life Balance" → also match "Work Life Balance"
            if (name.Contains('-'))
            {
                map[name.Replace("-", " ")] = name;
                map[name.Replace("-", " ").ToLowerInvariant()] = name;
                map[name.Replace("-", "_").ToLowerInvariant()] = name;
                map[name.Replace("-", "").ToLowerInvariant()] = name;
            }
        }
        return map;
    }

    internal static string NormalizeDimensionName(string raw)
    {
        if (DimensionNameMap.TryGetValue(raw, out var canonical))
            return canonical;
        var spacified = raw.Replace("_", " ");
        if (DimensionNameMap.TryGetValue(spacified, out canonical))
            return canonical;
        // Try stripping hyphens/special chars
        var stripped = spacified.Replace("-", " ").Replace("&", "And");
        if (DimensionNameMap.TryGetValue(stripped, out canonical))
            return canonical;
        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spacified);
    }

    // ── Export prompts as JSONL for batch API ──

    public async Task ExportPromptsAsync(string? filterDimension, string? filterMode, string? filterChemical, string modelId)
    {
        var requests = BuildRequests(filterDimension, filterMode, filterChemical);
        if (requests.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No requests to export.[/]");
            return;
        }

        Directory.CreateDirectory(OutputDir);
        var outPath = Path.Combine(OutputDir, "batch_requests.jsonl");

        await using var writer = new StreamWriter(outPath);
        foreach (var r in requests)
        {
            var obj = new
            {
                custom_id = r.CustomId,
                @params = new
                {
                    model = modelId,
                    max_tokens = 16384,
                    system = r.SystemPrompt,
                    messages = new[] { new { role = "user", content = r.UserPrompt } }
                }
            };
            var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = false });
            await writer.WriteLineAsync(json);
        }

        AnsiConsole.MarkupLine($"[green]Exported {requests.Count} requests to {outPath}[/]");
        AnsiConsole.MarkupLine("[dim]Submit with: .\\run_batch_submit.ps1[/]");
        AnsiConsole.MarkupLine("[dim]Collect with: .\\run_batch_collect.ps1[/]");
    }

    // ── Assemble results from raw_responses into ShadowProfiles.yaml ──

    public async Task AssembleAsync()
    {
        var rawDir = Path.Combine(OutputDir, "raw_responses");
        if (!Directory.Exists(rawDir))
        {
            AnsiConsole.MarkupLine("[red]No raw_responses directory found.[/]");
            return;
        }

        var files = Directory.GetFiles(rawDir, "*.txt");
        if (files.Length == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No response files found in raw_responses/.[/]");
            return;
        }

        var allFragments = new Dictionary<string, Dictionary<string, Dictionary<int, Dictionary<string, string>>>>();
        var generated = 0;
        var failed = 0;

        foreach (var file in files.OrderBy(f => f))
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            // Files use __ separator: dopamine__work.txt, oxytocin_h__private.txt
            // Fall back to last _ for legacy files: dopamine_work.txt
            string chemical, mode;
            var dblIdx = fileName.IndexOf("__", StringComparison.Ordinal);
            if (dblIdx > 0)
            {
                chemical = fileName[..dblIdx];
                mode = fileName[(dblIdx + 2)..];
            }
            else
            {
                // Legacy: split on last underscore
                var lastUnderscore = fileName.LastIndexOf('_');
                if (lastUnderscore <= 0) { failed++; continue; }
                chemical = fileName[..lastUnderscore];
                mode = fileName[(lastUnderscore + 1)..];
            }
            if (string.IsNullOrEmpty(chemical) || string.IsNullOrEmpty(mode)) { failed++; continue; }

            var responseText = await File.ReadAllTextAsync(file);
            var count = MergeFragments(allFragments, responseText, chemical, mode);
            generated += count;
            AnsiConsole.MarkupLine($"  {chemical}/{mode}: [green]{count} entries[/]");
        }

        var serializer = new SerializerBuilder().Build();
        var yaml = serializer.Serialize(allFragments);
        var outPath = Path.Combine(OutputDir, "ShadowProfiles.yaml");
        await File.WriteAllTextAsync(outPath, yaml);

        AnsiConsole.MarkupLine($"[green]Assembled: {generated} entries from {files.Length} files, {failed} skipped[/]");
        AnsiConsole.MarkupLine($"[green]Written: {outPath}[/]");
    }

    // ── Synchronous generation (existing) ──

    public async Task GenerateAsync(string? filterDimension, string? filterMode, bool dryRun, string? filterChemical)
    {
        var requests = BuildRequests(filterDimension, filterMode, filterChemical);

        if (dryRun)
        {
            foreach (var r in requests)
            {
                AnsiConsole.MarkupLine($"[bold]=== {r.Chemical} / {r.Mode} ===[/]");
                AnsiConsole.MarkupLine("[dim]-- System --[/]");
                AnsiConsole.WriteLine(r.SystemPrompt[..Math.Min(500, r.SystemPrompt.Length)] + "...");
                AnsiConsole.MarkupLine("[dim]-- User --[/]");
                AnsiConsole.WriteLine(r.UserPrompt[..Math.Min(500, r.UserPrompt.Length)] + "...");
                AnsiConsole.WriteLine();
            }
            AnsiConsole.MarkupLine($"[green]Dry run complete. {requests.Count} requests.[/]");
            return;
        }

        if (chatClient is null)
        {
            AnsiConsole.MarkupLine("[red]No LLM provider configured. Set Llm config in appsettings or use --dry-run.[/]");
            return;
        }

        var allFragments = new Dictionary<string, Dictionary<string, Dictionary<int, Dictionary<string, string>>>>();
        var generated = 0;
        var failed = 0;

        var rawDir = Path.Combine(OutputDir, "raw_responses");
        Directory.CreateDirectory(rawDir);

        foreach (var r in requests)
        {
            try
            {
                AnsiConsole.MarkupLine($"[dim]Generating: {r.Chemical} / {r.Mode}...[/]");
                var responseText = await CallLlm(r.SystemPrompt, r.UserPrompt);
                await File.WriteAllTextAsync(Path.Combine(rawDir, $"{r.Chemical}__{r.Mode}.txt"), responseText);

                var count = MergeFragments(allFragments, responseText, r.Chemical, r.Mode);
                generated += count;
                AnsiConsole.MarkupLine($"  [green]Parsed {count} entries[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed: {r.Chemical}/{r.Mode} - {ex.Message}[/]");
                failed++;
            }
        }

        Directory.CreateDirectory(OutputDir);
        var serializer = new SerializerBuilder().Build();
        var yaml = serializer.Serialize(allFragments);
        var outPath = Path.Combine(OutputDir, "ShadowProfiles.yaml");
        await File.WriteAllTextAsync(outPath, yaml);

        AnsiConsole.MarkupLine($"[green]Generated: {generated} entries, Failed: {failed}[/]");
        AnsiConsole.MarkupLine($"[green]Written: {outPath}[/]");
    }

    // ── Shared helpers ──

    private record GenerationRequest(string CustomId, string Chemical, string Mode, string SystemPrompt, string UserPrompt);

    private List<GenerationRequest> BuildRequests(string? filterDimension, string? filterMode, string? filterChemical)
    {
        var chemicals = promptBuilder.GetAllChemicals();
        if (filterChemical != null)
            chemicals = chemicals.Where(c => c.Equals(filterChemical, StringComparison.OrdinalIgnoreCase)).ToList();

        var modes = new[] { "work", "private" };
        if (filterMode != null)
            modes = [filterMode];

        var requests = new List<GenerationRequest>();
        foreach (var chemical in chemicals)
        {
            var dims = promptBuilder.GetDimensionsForChemical(chemical);
            if (filterDimension != null)
                dims = dims.Where(d => d.Name.Equals(filterDimension, StringComparison.OrdinalIgnoreCase)).ToList();
            if (dims.Count == 0) continue;

            var systemPrompt = promptBuilder.BuildSystemPrompt(chemical);
            foreach (var mode in modes)
            {
                var userPrompt = promptBuilder.BuildUserPrompt(chemical, mode, filterDimension);
                requests.Add(new GenerationRequest($"{chemical}__{mode}", chemical, mode, systemPrompt, userPrompt));
            }
        }
        return requests;
    }

    private async Task<string> CallLlm(string systemPrompt, string userPrompt)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt)
        };
        var options = new ChatOptions { MaxOutputTokens = 16384 };
        var response = await chatClient!.GetResponseAsync(messages, options);
        return response.Text ?? "";
    }

    internal static int MergeFragments(
        Dictionary<string, Dictionary<string, Dictionary<int, Dictionary<string, string>>>> allFragments,
        string responseText, string chemical, string mode)
    {
        var parsed = ParseResponse(responseText, chemical);
        var count = 0;
        foreach (var (dimName, levels) in parsed)
        {
            if (!allFragments.TryGetValue(dimName, out var modeDict))
            {
                modeDict = new Dictionary<string, Dictionary<int, Dictionary<string, string>>>();
                allFragments[dimName] = modeDict;
            }
            if (!modeDict.TryGetValue(mode, out var levelDict))
            {
                levelDict = new Dictionary<int, Dictionary<string, string>>();
                modeDict[mode] = levelDict;
            }
            foreach (var (level, text) in levels)
            {
                if (!levelDict.TryGetValue(level, out var chemDict))
                {
                    chemDict = new Dictionary<string, string>();
                    levelDict[level] = chemDict;
                }
                chemDict[chemical] = text;
                count++;
            }
        }
        return count;
    }

    internal static Dictionary<string, Dictionary<int, string>> ParseResponse(string responseText, string chemical)
    {
        var result = TryParseYaml(responseText, chemical);
        if (result.Count > 0) return result;
        return ParseWithRegex(responseText, chemical);
    }

    private static Dictionary<string, Dictionary<int, string>> TryParseYaml(string responseText, string chemical)
    {
        var result = new Dictionary<string, Dictionary<int, string>>();
        try
        {
            var yamlText = ExtractYamlBlock(responseText);
            var deserializer = new DeserializerBuilder().Build();
            var parsed = deserializer.Deserialize<Dictionary<string, object>>(yamlText);
            if (parsed == null) return result;

            foreach (var (rawDimName, dimValue) in parsed)
            {
                if (dimValue is not Dictionary<object, object> levels) continue;
                var dimName = NormalizeDimensionName(rawDimName);
                var dimLevels = new Dictionary<int, string>();

                foreach (var (levelKey, levelValue) in levels)
                {
                    if (!int.TryParse(levelKey.ToString(), out var level)) continue;

                    if (levelValue is Dictionary<object, object> chemDict)
                    {
                        foreach (var (chemKey, chemValue) in chemDict)
                        {
                            if (chemKey.ToString()?.Equals(chemical, StringComparison.OrdinalIgnoreCase) == true)
                                dimLevels[level] = chemValue?.ToString()?.Trim() ?? "";
                        }
                    }
                    else if (levelValue is string text)
                    {
                        dimLevels[level] = text.Trim();
                    }
                }

                if (dimLevels.Count > 0)
                    result[dimName] = dimLevels;
            }
        }
        catch { }
        return result;
    }

    private static Dictionary<string, Dictionary<int, string>> ParseWithRegex(string responseText, string chemical)
    {
        var result = new Dictionary<string, Dictionary<int, string>>();
        var text = ExtractYamlBlock(responseText);

        string? currentDim = null;
        int? currentLevel = null;
        var currentText = new List<string>();
        var inChemBlock = false;

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.TrimEnd();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            var indent = line.Length - line.TrimStart().Length;

            if (indent == 0 && trimmed.EndsWith(':') && !char.IsDigit(trimmed[0]))
            {
                FlushEntry(result, currentDim, currentLevel, currentText);
                currentDim = NormalizeDimensionName(trimmed[..^1].Trim());
                currentLevel = null;
                currentText.Clear();
                inChemBlock = false;
                continue;
            }

            if (indent >= 1 && indent <= 4)
            {
                var levelMatch = Regex.Match(trimmed, @"^(\d+)\s*:");
                if (levelMatch.Success)
                {
                    FlushEntry(result, currentDim, currentLevel, currentText);
                    currentLevel = int.Parse(levelMatch.Groups[1].Value);
                    currentText.Clear();
                    inChemBlock = false;
                    continue;
                }
            }

            if (indent >= 3 && indent <= 8)
            {
                var chemMatch = Regex.Match(trimmed, @"^(\w[\w\s-]*?)\s*:\s*[>|]?\s*(.*)$");
                if (chemMatch.Success)
                {
                    var foundChem = chemMatch.Groups[1].Value.Trim();
                    if (foundChem.Equals(chemical, StringComparison.OrdinalIgnoreCase))
                    {
                        currentText.Clear();
                        inChemBlock = true;
                        var inlineText = chemMatch.Groups[2].Value.Trim();
                        if (!string.IsNullOrEmpty(inlineText) && inlineText != ">" && inlineText != "|")
                            currentText.Add(inlineText);
                        continue;
                    }
                    else if (inChemBlock)
                    {
                        FlushEntry(result, currentDim, currentLevel, currentText);
                        inChemBlock = false;
                    }
                }
            }

            if (indent >= 5 && inChemBlock && currentDim != null && currentLevel != null)
                currentText.Add(trimmed);
        }

        FlushEntry(result, currentDim, currentLevel, currentText);
        return result;
    }

    private static void FlushEntry(Dictionary<string, Dictionary<int, string>> result,
        string? dim, int? level, List<string> textLines)
    {
        if (dim == null || level == null || textLines.Count == 0) return;
        var text = string.Join(" ", textLines).Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        if (!result.TryGetValue(dim, out var levels))
        {
            levels = new Dictionary<int, string>();
            result[dim] = levels;
        }
        levels[level.Value] = text;
    }

    private static string ExtractYamlBlock(string responseText)
    {
        var fenceStart = responseText.IndexOf("```yaml", StringComparison.OrdinalIgnoreCase);
        if (fenceStart >= 0)
        {
            var contentStart = responseText.IndexOf('\n', fenceStart) + 1;
            var fenceEnd = responseText.IndexOf("```", contentStart, StringComparison.Ordinal);
            if (fenceEnd > contentStart)
                return responseText[contentStart..fenceEnd];
        }

        fenceStart = responseText.IndexOf("```", StringComparison.Ordinal);
        if (fenceStart >= 0)
        {
            var contentStart = responseText.IndexOf('\n', fenceStart) + 1;
            var fenceEnd = responseText.IndexOf("```", contentStart, StringComparison.Ordinal);
            if (fenceEnd > contentStart)
                return responseText[contentStart..fenceEnd];
        }

        return responseText;
    }
}
