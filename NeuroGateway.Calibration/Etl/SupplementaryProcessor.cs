using System.Globalization;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Spectre.Console;

namespace NeuroGateway.Calibration.Etl;

public class SupplementaryProcessor
{
    private static readonly string RawDataDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "NeuroGateway.Calibration", "RawData");
    private static readonly string OutputDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "NeuroGateway.Calibration", "Outputs");

    public async Task ProcessAsync()
    {
        var signatures = new Dictionary<string, Dictionary<string, object>>();

        // Process DASS-42
        var dassPath = FindDataset("DASS");
        if (dassPath != null)
        {
            AnsiConsole.MarkupLine($"[dim]Processing DASS-42: {dassPath}[/]");
            await ProcessDassAsync(dassPath, signatures);
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]DASS-42 dataset not found (optional).[/]");
        }

        // Process Short Dark Triad
        var sd3Path = FindDataset("SD3");
        if (sd3Path != null)
        {
            AnsiConsole.MarkupLine($"[dim]Processing SD3: {sd3Path}[/]");
            await ProcessSd3Async(sd3Path, signatures);
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Short Dark Triad dataset not found (optional).[/]");
        }

        // Process RIASEC
        var riasecPath = FindDataset("RIASEC");
        if (riasecPath != null)
        {
            AnsiConsole.MarkupLine($"[dim]Processing RIASEC: {riasecPath}[/]");
            await ProcessRiasecAsync(riasecPath, signatures);
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]RIASEC dataset not found (optional).[/]");
        }

        if (signatures.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No supplementary datasets processed.[/]");
            return;
        }

        Directory.CreateDirectory(OutputDir);
        var output = new Dictionary<string, object>
        {
            ["source"] = "Supplementary (DASS-42, SD3, RIASEC)",
            ["dimensions"] = signatures
        };

        var outPath = Path.Combine(OutputDir, "behavioral_signatures.json");
        var json = JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(outPath, json);
        AnsiConsole.MarkupLine($"[green]Written: {outPath} ({signatures.Count} dimensions)[/]");
    }

    private static async Task ProcessDassAsync(string path, Dictionary<string, Dictionary<string, object>> signatures)
    {
        // DASS-42: columns Q1A-Q42A (answer values), scale 1-4 (mapped from 0-3 in some versions)
        // 3 subscales: Depression, Anxiety, Stress — each 14 items
        var subscaleScores = new List<(double Depression, double Anxiety, double Stress)>();
        var delimiter = await DetectDelimiter(path);

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true, MissingFieldFound = null, BadDataFound = null, Delimiter = delimiter
        });

        await csv.ReadAsync();
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? [];

        // Depression: 3,5,10,13,16,17,21,24,26,31,34,37,38,42
        var depItems = new[] { 3, 5, 10, 13, 16, 17, 21, 24, 26, 31, 34, 37, 38, 42 };
        var anxItems = new[] { 2, 4, 7, 9, 15, 19, 20, 23, 25, 28, 30, 36, 40, 41 };
        var strItems = new[] { 1, 6, 8, 11, 12, 14, 18, 22, 27, 29, 32, 33, 35, 39 };

        while (await csv.ReadAsync())
        {
            var items = new double[42];
            var valid = true;
            for (var i = 1; i <= 42; i++)
            {
                // DASS columns: Q1A, Q2A, ..., Q42A
                var colName = $"Q{i}A";
                if (!headers.Contains(colName)) { valid = false; break; }
                var raw = csv.GetField(colName);
                if (!double.TryParse(raw, CultureInfo.InvariantCulture, out var val)) { valid = false; break; }
                // Dataset uses 1-4 scale; normalize to 0-3 for standard DASS scoring
                if (val >= 1 && val <= 4) val -= 1;
                items[i - 1] = val;
            }
            if (!valid) continue;

            // Subscale = sum of items × 2
            var dep = depItems.Sum(i => items[i - 1]) * 2;
            var anx = anxItems.Sum(i => items[i - 1]) * 2;
            var str = strItems.Sum(i => items[i - 1]) * 2;

            subscaleScores.Add((dep, anx, str));
        }

        if (subscaleScores.Count > 0)
        {
            AddSignature(signatures, "Stress Management", "dass_stress",
                $"N={subscaleScores.Count}, mean stress={subscaleScores.Average(s => s.Stress):F1}");
            AddSignature(signatures, "Emotional Stability", "dass_anxiety",
                $"N={subscaleScores.Count}, mean anxiety={subscaleScores.Average(s => s.Anxiety):F1}");
            AddSignature(signatures, "Resilience", "dass_depression",
                $"N={subscaleScores.Count}, mean depression={subscaleScores.Average(s => s.Depression):F1}");
            AnsiConsole.MarkupLine($"  [green]{subscaleScores.Count:N0} respondents processed[/]");
        }
    }

    private static async Task ProcessSd3Async(string path, Dictionary<string, Dictionary<string, object>> signatures)
    {
        // Short Dark Triad: columns M1-M9 (Machiavellianism), N1-N9 (Narcissism), P1-P9 (Psychopathy)
        // Scale 1-5
        var scores = new List<(double Mach, double Narc, double Psych)>();
        var delimiter = await DetectDelimiter(path);

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true, MissingFieldFound = null, BadDataFound = null, Delimiter = delimiter
        });

        await csv.ReadAsync();
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? [];

        while (await csv.ReadAsync())
        {
            var mItems = new double[9];
            var nItems = new double[9];
            var pItems = new double[9];
            var valid = true;

            for (var i = 1; i <= 9; i++)
            {
                var mCol = $"M{i}";
                var nCol = $"N{i}";
                var pCol = $"P{i}";

                if (!headers.Contains(mCol) || !headers.Contains(nCol) || !headers.Contains(pCol))
                { valid = false; break; }

                var mRaw = csv.GetField(mCol);
                var nRaw = csv.GetField(nCol);
                var pRaw = csv.GetField(pCol);

                if (!double.TryParse(mRaw, CultureInfo.InvariantCulture, out var mVal) || mVal < 1 || mVal > 5 ||
                    !double.TryParse(nRaw, CultureInfo.InvariantCulture, out var nVal) || nVal < 1 || nVal > 5 ||
                    !double.TryParse(pRaw, CultureInfo.InvariantCulture, out var pVal) || pVal < 1 || pVal > 5)
                { valid = false; break; }

                mItems[i - 1] = mVal;
                nItems[i - 1] = nVal;
                pItems[i - 1] = pVal;
            }
            if (!valid) continue;

            scores.Add((mItems.Average(), nItems.Average(), pItems.Average()));
        }

        if (scores.Count > 0)
        {
            AddSignature(signatures, "Leadership", "sd3_narcissism",
                $"N={scores.Count}, mean narcissism={scores.Average(s => s.Narc):F2}");
            AddSignature(signatures, "Influence", "sd3_machiavellianism",
                $"N={scores.Count}, mean mach={scores.Average(s => s.Mach):F2}");
            AddSignature(signatures, "Risk Tolerance", "sd3_psychopathy",
                $"N={scores.Count}, mean psychopathy={scores.Average(s => s.Psych):F2}");
            AnsiConsole.MarkupLine($"  [green]{scores.Count:N0} respondents processed[/]");
        }
    }

    private static async Task ProcessRiasecAsync(string path, Dictionary<string, Dictionary<string, object>> signatures)
    {
        // RIASEC: columns R1-R8, I1-I8, A1-A8, S1-S8, E1-E8, C1-C8 (48 items total)
        // Scale 1-5
        var typeLabels = new[] { ("R", "Realistic"), ("I", "Investigative"), ("A", "Artistic"),
            ("S", "Social"), ("E", "Enterprising"), ("C", "Conventional") };
        var scores = new List<Dictionary<string, double>>();
        var delimiter = await DetectDelimiter(path);

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true, MissingFieldFound = null, BadDataFound = null, Delimiter = delimiter
        });

        await csv.ReadAsync();
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? [];

        while (await csv.ReadAsync())
        {
            var typeScores = new Dictionary<string, double>();
            var valid = true;

            foreach (var (prefix, typeName) in typeLabels)
            {
                double sum = 0;
                var count = 0;
                for (var i = 1; i <= 8; i++)
                {
                    var colName = $"{prefix}{i}";
                    if (!headers.Contains(colName)) continue;
                    var raw = csv.GetField(colName);
                    if (string.IsNullOrWhiteSpace(raw) || raw == "0") continue;
                    if (!double.TryParse(raw, CultureInfo.InvariantCulture, out var val) || val < 1 || val > 5) continue;
                    sum += val;
                    count++;
                }
                if (count < 4) { valid = false; break; }
                typeScores[typeName] = sum / count;
            }

            if (!valid) continue;
            scores.Add(typeScores);
        }

        if (scores.Count > 0)
        {
            var archetypeMap = new Dictionary<string, string[]>
            {
                ["Investigative"] = ["Strategic Thinking", "Analytical Thinking"],
                ["Artistic"] = ["Creativity", "Innovation"],
                ["Social"] = ["Empathy", "Team Orientation"],
                ["Enterprising"] = ["Leadership", "Influence"],
                ["Conventional"] = ["Detail Orientation", "Persistence"]
            };

            foreach (var (type, dims) in archetypeMap)
            {
                var mean = scores.Average(s => s[type]);
                foreach (var dim in dims)
                    AddSignature(signatures, dim, $"riasec_{type.ToLowerInvariant()}", $"N={scores.Count}, mean {type}={mean:F2}");
            }

            AnsiConsole.MarkupLine($"  [green]{scores.Count:N0} respondents processed[/]");
        }
    }

    private static void AddSignature(Dictionary<string, Dictionary<string, object>> signatures,
        string dimension, string key, string value)
    {
        if (!signatures.TryGetValue(dimension, out var dimSigs))
        {
            dimSigs = new Dictionary<string, object>();
            signatures[dimension] = dimSigs;
        }
        dimSigs[key] = value;
    }

    private static async Task<string> DetectDelimiter(string path)
    {
        var firstLine = (await File.ReadAllLinesAsync(path)).FirstOrDefault() ?? "";
        return firstLine.Contains('\t') ? "\t" : ",";
    }

    private static string? FindDataset(string name)
    {
        var dir = Path.GetFullPath(RawDataDir);
        if (!Directory.Exists(dir)) return null;

        foreach (var ext in new[] { ".csv", ".txt" })
        {
            var files = Directory.GetFiles(dir, $"*{name}*{ext}", SearchOption.AllDirectories);
            if (files.Length > 0) return files[0];
        }

        // Also search for data.csv inside subdirectories named after the dataset
        var subdirs = Directory.GetDirectories(dir, $"*{name}*", SearchOption.TopDirectoryOnly);
        foreach (var subdir in subdirs)
        {
            var dataFile = Path.Combine(subdir, "data.csv");
            if (File.Exists(dataFile)) return dataFile;
        }

        return null;
    }
}
