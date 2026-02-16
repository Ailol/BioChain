using System.Globalization;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Spectre.Console;

namespace NeuroGateway.Calibration.Etl;

public class OnetProcessor
{
    private static readonly string RawDataDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "NeuroGateway.Calibration", "RawData");
    private static readonly string OutputDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "NeuroGateway.Calibration", "Outputs");

    // O*NET Work Style → NeuroGateway dimension mapping
    private static readonly Dictionary<string, string[]> WorkStyleToDimensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Achievement/Effort"] = ["Ambition", "Drive"],
        ["Persistence"] = ["Resilience", "Drive"],
        ["Initiative"] = ["Innovation", "Influence"],
        ["Leadership"] = ["Leadership", "Strategic Thinking"],
        ["Cooperation"] = ["Team Orientation", "Conflict Resolution"],
        ["Concern for Others"] = ["Empathy", "Social Intelligence"],
        ["Social Orientation"] = ["Communication", "Team Orientation"],
        ["Self-Control"] = ["Emotional Stability", "Patience"],
        ["Self Control"] = ["Emotional Stability", "Patience"],
        ["Stress Tolerance"] = ["Stress Management", "Resilience"],
        ["Adaptability/Flexibility"] = ["Adaptability", "Learning Agility"],
        ["Dependability"] = ["Detail Orientation", "Authenticity"],
        ["Attention to Detail"] = ["Detail Orientation", "Analytical Thinking"],
        ["Integrity"] = ["Trust Building", "Authenticity"],
        ["Independence"] = ["Risk Tolerance", "Self-Awareness"],
        ["Innovation"] = ["Creativity", "Innovation"],
        ["Analytical Thinking"] = ["Analytical Thinking", "Strategic Thinking"],
    };

    public async Task ProcessAsync()
    {
        var onetDir = Path.Combine(Path.GetFullPath(RawDataDir), "onet");
        if (!Directory.Exists(onetDir))
        {
            AnsiConsole.MarkupLine("[yellow]O*NET data not found.[/]");
            AnsiConsole.MarkupLine("Download from: [link]https://www.onetcenter.org/database.html[/]");
            AnsiConsole.MarkupLine($"Save to: [dim]{onetDir}[/]");
            AnsiConsole.MarkupLine("Expected files: Work Styles.txt (pipe-delimited)");
            return;
        }

        var workStylesFile = FindFile(onetDir, "Work Styles");
        if (workStylesFile == null)
        {
            AnsiConsole.MarkupLine("[yellow]Work Styles file not found in O*NET directory.[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[dim]Loading: {workStylesFile}[/]");

        var dimensionLevels = new Dictionary<string, Dictionary<int, List<string>>>();

        using var reader = new StreamReader(workStylesFile);
        var delimiter = workStylesFile.EndsWith(".txt") ? "|" : ",";
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            Delimiter = delimiter,
            MissingFieldFound = null,
            BadDataFound = null
        });

        await csv.ReadAsync();
        csv.ReadHeader();

        while (await csv.ReadAsync())
        {
            var elementName = csv.GetField("Element Name") ?? csv.GetField(1) ?? "";
            var scaleName = csv.GetField("Scale Name") ?? csv.GetField(3) ?? "";
            var dataValue = csv.GetField("Data Value") ?? csv.GetField(4) ?? "";
            var occupation = csv.GetField("Title") ?? csv.GetField("O*NET-SOC Title") ?? "";

            if (!double.TryParse(dataValue, CultureInfo.InvariantCulture, out var value)) continue;

            // Only process "Importance" scale
            if (!scaleName.Contains("Importance", StringComparison.OrdinalIgnoreCase) &&
                !scaleName.Contains("IM", StringComparison.OrdinalIgnoreCase))
                continue;

            // Map work style to dimensions
            if (!WorkStyleToDimensions.TryGetValue(elementName.Trim(), out var dims)) continue;

            // Convert 1-5 importance to level 1-5
            var level = Math.Clamp((int)Math.Round(value), 1, 5);

            foreach (var dim in dims)
            {
                if (!dimensionLevels.TryGetValue(dim, out var levels))
                {
                    levels = new Dictionary<int, List<string>>();
                    dimensionLevels[dim] = levels;
                }
                if (!levels.TryGetValue(level, out var occupations))
                {
                    occupations = [];
                    levels[level] = occupations;
                }
                if (occupations.Count < 5 && !string.IsNullOrWhiteSpace(occupation))
                    occupations.Add(occupation);
            }
        }

        // Build output
        var output = new Dictionary<string, object>
        {
            ["source"] = "O*NET",
            ["dimensions"] = dimensionLevels.ToDictionary(
                kv => kv.Key,
                kv => (object)new Dictionary<string, object>
                {
                    ["levels"] = kv.Value.OrderBy(l => l.Key).ToDictionary(
                        l => l.Key.ToString(),
                        l => (object)new Dictionary<string, object>
                        {
                            ["occupations"] = l.Value.Take(5).ToArray()
                        })
                })
        };

        Directory.CreateDirectory(OutputDir);
        var outPath = Path.Combine(OutputDir, "work_behavioral_signatures.json");
        var json = JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(outPath, json);
        AnsiConsole.MarkupLine($"[green]Written: {outPath} ({dimensionLevels.Count} dimensions)[/]");
    }

    private static string? FindFile(string dir, string pattern)
    {
        foreach (var ext in new[] { ".txt", ".csv", ".xlsx" })
        {
            var files = Directory.GetFiles(dir, $"*{pattern}*{ext}", SearchOption.AllDirectories);
            if (files.Length > 0) return files[0];
        }
        return null;
    }
}
