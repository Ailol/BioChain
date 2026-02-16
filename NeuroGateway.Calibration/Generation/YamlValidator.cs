using NeuroGateway.AnalysisFramework;
using Spectre.Console;
using YamlDotNet.Serialization;

namespace NeuroGateway.Calibration.Generation;

public class YamlValidator
{
    private static readonly string OutputDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "NeuroGateway.Calibration", "Outputs");
    private static readonly HashSet<string> KnownChemicals = new(DimensionDefinitions.ChemicalToLayer.Keys, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> KnownDimensions = new(DimensionDefinitions.All.Select(d => d.Name));

    public Task ValidateAsync(string? path = null)
    {
        path ??= Path.Combine(OutputDir, "ShadowProfiles.yaml");
        if (!File.Exists(path))
        {
            // Try AnalysisFramework location
            var altPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "NeuroGateway.AnalysisFramework", "Constants", "ShadowProfiles.yaml");
            if (File.Exists(altPath))
                path = altPath;
            else
            {
                AnsiConsole.MarkupLine($"[red]File not found: {path}[/]");
                return Task.CompletedTask;
            }
        }

        AnsiConsole.MarkupLine($"[dim]Validating: {path}[/]");

        string yamlText;
        try
        {
            yamlText = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Cannot read file: {ex.Message}[/]");
            return Task.CompletedTask;
        }

        // Parse
        Dictionary<string, object>? parsed;
        try
        {
            var deserializer = new DeserializerBuilder().Build();
            parsed = deserializer.Deserialize<Dictionary<string, object>>(yamlText);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]YAML parse error: {ex.Message}[/]");
            return Task.CompletedTask;
        }

        if (parsed == null)
        {
            AnsiConsole.MarkupLine("[red]YAML parsed as null.[/]");
            return Task.CompletedTask;
        }

        var dimensionsPresent = 0;
        var modeVariants = 0;
        var levelEntries = 0;
        var chemicalEntries = 0;
        var invalidChemicals = new List<string>();
        var shortEntries = new List<string>();
        var missingDimensions = new List<string>();
        var missingModes = new List<string>();

        foreach (var dimName in KnownDimensions)
        {
            if (!parsed.ContainsKey(dimName))
            {
                missingDimensions.Add(dimName);
                continue;
            }
            dimensionsPresent++;

            var dimValue = parsed[dimName];
            if (dimValue is not Dictionary<object, object> modes)
            {
                missingModes.Add($"{dimName} (not a mapping)");
                continue;
            }

            foreach (var modeName in new[] { "work", "private" })
            {
                if (!modes.ContainsKey(modeName))
                {
                    missingModes.Add($"{dimName}.{modeName}");
                    continue;
                }
                modeVariants++;

                if (modes[modeName] is not Dictionary<object, object> levels) continue;

                foreach (var levelKey in new[] { "1", "2", "3", "4", "5" })
                {
                    if (!levels.ContainsKey(levelKey) && !levels.ContainsKey(int.Parse(levelKey)))
                        continue;

                    levelEntries++;
                    var levelValue = levels.TryGetValue(levelKey, out var lv) ? lv : levels[int.Parse(levelKey)];

                    if (levelValue is Dictionary<object, object> chemDict)
                    {
                        foreach (var (chemKey, chemValue) in chemDict)
                        {
                            var chemName = chemKey.ToString() ?? "";
                            if (!KnownChemicals.Contains(chemName))
                                invalidChemicals.Add($"{dimName}.{modeName}.{levelKey}.{chemName}");

                            var text = chemValue?.ToString() ?? "";
                            chemicalEntries++;
                            if (text.Length < 50)
                                shortEntries.Add($"{dimName}.{modeName}.{levelKey}.{chemName} ({text.Length} chars)");
                        }
                    }
                }
            }
        }

        // Report
        AnsiConsole.WriteLine();
        var total24 = KnownDimensions.Count;
        var check = dimensionsPresent == total24 ? "[green]OK[/]" : "[yellow]PARTIAL[/]";
        AnsiConsole.MarkupLine($"{check} {dimensionsPresent}/{total24} dimensions present");

        var total48 = total24 * 2;
        check = modeVariants == total48 ? "[green]OK[/]" : "[yellow]PARTIAL[/]";
        AnsiConsole.MarkupLine($"{check} {modeVariants}/{total48} mode variants present");

        AnsiConsole.MarkupLine($"[green]OK[/] {levelEntries} level entries present");
        AnsiConsole.MarkupLine($"[green]OK[/] {chemicalEntries} chemical entries valid");

        if (missingDimensions.Count > 0)
        {
            AnsiConsole.MarkupLine($"[yellow]Missing dimensions ({missingDimensions.Count}):[/]");
            foreach (var d in missingDimensions.Take(10))
                AnsiConsole.MarkupLine($"  - {d}");
        }

        if (missingModes.Count > 0)
        {
            AnsiConsole.MarkupLine($"[yellow]Missing modes ({missingModes.Count}):[/]");
            foreach (var m in missingModes.Take(10))
                AnsiConsole.MarkupLine($"  - {m}");
        }

        if (invalidChemicals.Count > 0)
        {
            AnsiConsole.MarkupLine($"[red]Invalid chemicals ({invalidChemicals.Count}):[/]");
            foreach (var c in invalidChemicals.Take(10))
                AnsiConsole.MarkupLine($"  - {c}");
        }

        if (shortEntries.Count > 0)
        {
            AnsiConsole.MarkupLine($"[yellow]Short entries ({shortEntries.Count}):[/]");
            foreach (var s in shortEntries.Take(10))
                AnsiConsole.MarkupLine($"  - {s}");
        }

        AnsiConsole.WriteLine();
        if (invalidChemicals.Count == 0 && missingDimensions.Count == 0)
            AnsiConsole.MarkupLine("[green]Validation passed.[/]");
        else
            AnsiConsole.MarkupLine("[yellow]Validation completed with warnings.[/]");

        return Task.CompletedTask;
    }
}
