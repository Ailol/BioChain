using System.Globalization;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Spectre.Console;

namespace NeuroGateway.Calibration.Etl;

public class IpipNeoProcessor
{
    private static readonly string RawDataDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "NeuroGateway.Calibration", "RawData");
    private static readonly string OutputDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "NeuroGateway.Calibration", "Outputs");

    // IPIP-FFM 50-item Big Five: 5 domains × 10 items
    // Domain prefixes: EXT (Extraversion), AGR (Agreeableness), CSN (Conscientiousness), EST (Emotional Stability/Neuroticism reversed), OPN (Openness)
    // Even-numbered items are reverse-scored within each domain
    private static readonly Dictionary<string, (string[] Items, HashSet<string> Reversed)> DomainKeys = new()
    {
        ["Extraversion"] = (
            Enumerable.Range(1, 10).Select(i => $"EXT{i}").ToArray(),
            new HashSet<string> { "EXT2", "EXT4", "EXT6", "EXT8", "EXT10" }),
        ["Agreeableness"] = (
            Enumerable.Range(1, 10).Select(i => $"AGR{i}").ToArray(),
            new HashSet<string> { "AGR1", "AGR3", "AGR5", "AGR7" }),
        ["Conscientiousness"] = (
            Enumerable.Range(1, 10).Select(i => $"CSN{i}").ToArray(),
            new HashSet<string> { "CSN2", "CSN4", "CSN6", "CSN8" }),
        ["EmotionalStability"] = (
            Enumerable.Range(1, 10).Select(i => $"EST{i}").ToArray(),
            new HashSet<string> { "EST2", "EST4" }),
        ["Openness"] = (
            Enumerable.Range(1, 10).Select(i => $"OPN{i}").ToArray(),
            new HashSet<string> { "OPN2", "OPN4", "OPN6" })
    };

    // Map Big Five domains to our 24 dimensions
    private static readonly Dictionary<string, string[]> DomainToDimensions = new()
    {
        ["Extraversion"] = ["Ambition", "Leadership", "Influence", "Communication Style"],
        ["Agreeableness"] = ["Empathy", "Team Orientation", "Conflict Resolution"],
        ["Conscientiousness"] = ["Detail Orientation", "Persistence", "Strategic Thinking"],
        ["EmotionalStability"] = ["Emotional Stability", "Stress Management", "Resilience"],
        ["Openness"] = ["Creativity", "Innovation", "Adaptability", "Analytical Thinking"]
    };

    public async Task ProcessAsync()
    {
        var csvPath = FindCsvFile();
        if (csvPath == null)
        {
            AnsiConsole.MarkupLine("[yellow]IPIP-NEO-300 dataset not found.[/]");
            AnsiConsole.MarkupLine("Download from: [link]https://openpsychometrics.org/_rawdata/[/]");
            AnsiConsole.MarkupLine($"Save CSV to: [dim]{Path.GetFullPath(RawDataDir)}[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[dim]Loading: {csvPath}[/]");

        // Detect delimiter
        var firstLine = (await File.ReadAllLinesAsync(csvPath)).FirstOrDefault() ?? "";
        var delimiter = firstLine.Contains('\t') ? "\t" : ",";

        var respondents = new List<Dictionary<string, double>>();
        var skipped = 0;

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Processing respondents[/]");
                task.IsIndeterminate = true;

                using var reader = new StreamReader(csvPath);
                using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    MissingFieldFound = null,
                    BadDataFound = null,
                    Delimiter = delimiter
                });

                await csv.ReadAsync();
                csv.ReadHeader();
                var headers = csv.HeaderRecord ?? [];

                while (await csv.ReadAsync())
                {
                    var domainScores = new Dictionary<string, double>();
                    var allValid = true;

                    foreach (var (domain, (items, reversed)) in DomainKeys)
                    {
                        double sum = 0;
                        var count = 0;
                        foreach (var item in items)
                        {
                            if (!headers.Contains(item)) continue;
                            var raw = csv.GetField(item);
                            if (string.IsNullOrWhiteSpace(raw) || raw == "0") continue;
                            if (!double.TryParse(raw, CultureInfo.InvariantCulture, out var val) || val < 1 || val > 5) continue;
                            sum += reversed.Contains(item) ? 6 - val : val;
                            count++;
                        }
                        if (count < 5) { allValid = false; break; }
                        domainScores[domain] = sum / count;
                    }

                    if (!allValid || domainScores.Count < 5) { skipped++; continue; }

                    // Map domain scores to dimension composites
                    var dimScores = new Dictionary<string, double>();
                    foreach (var (domain, dims) in DomainToDimensions)
                    {
                        if (!domainScores.TryGetValue(domain, out var score)) continue;
                        foreach (var dim in dims)
                            dimScores[dim] = score;
                    }

                    if (dimScores.Count > 0)
                        respondents.Add(dimScores);

                    if (respondents.Count % 10000 == 0)
                        task.Description = $"[green]Processing respondents ({respondents.Count:N0})[/]";
                }

                task.Value = 100;
                task.StopTask();
            });

        AnsiConsole.MarkupLine($"[green]{respondents.Count:N0} respondents processed, {skipped:N0} skipped.[/]");

        // Compute boundaries
        var output = new Dictionary<string, object>
        {
            ["source"] = "IPIP-FFM-50",
            ["nRespondents"] = respondents.Count,
            ["processedAt"] = DateTime.UtcNow.ToString("O"),
            ["dimensions"] = ComputeBoundaries(respondents)
        };

        Directory.CreateDirectory(OutputDir);
        var outPath = Path.Combine(OutputDir, "percentile_boundaries.json");
        var json = JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(outPath, json);
        AnsiConsole.MarkupLine($"[green]Written: {outPath}[/]");
    }

    private string? FindCsvFile()
    {
        var dir = Path.GetFullPath(RawDataDir);
        if (!Directory.Exists(dir)) return null;

        var patterns = new[] { "*IPIP*FFM*data*.csv", "*IPIP*NEO*300*.csv", "data-final.csv", "data.csv" };
        foreach (var pattern in patterns)
        {
            var files = Directory.GetFiles(dir, pattern, SearchOption.AllDirectories);
            if (files.Length > 0) return files[0];
        }
        return null;
    }

    private static Dictionary<string, object> ComputeBoundaries(List<Dictionary<string, double>> respondents)
    {
        var result = new Dictionary<string, object>();
        var allDims = respondents.SelectMany(r => r.Keys).Distinct().ToList();

        foreach (var dimName in allDims)
        {
            var scores = respondents
                .Where(r => r.ContainsKey(dimName))
                .Select(r => r[dimName])
                .OrderBy(x => x)
                .ToList();

            if (scores.Count < 10) continue;

            var n = scores.Count;
            var quintiles = new[]
            {
                scores[(int)(n * 0.20)],
                scores[(int)(n * 0.40)],
                scores[(int)(n * 0.60)],
                scores[(int)(n * 0.80)]
            };

            var centroids = KMeans1D(scores, 5);

            var mean = scores.Average();
            var std = Math.Sqrt(scores.Sum(x => (x - mean) * (x - mean)) / n);
            var skew = n > 2 ? scores.Sum(x => Math.Pow((x - mean) / std, 3)) * n / ((n - 1.0) * (n - 2.0)) : 0;

            result[dimName] = new Dictionary<string, object>
            {
                ["quintileBoundaries"] = quintiles.Select(q => Math.Round(q, 2)).ToArray(),
                ["clusterCentroids"] = centroids.Select(c => Math.Round(c, 2)).ToArray(),
                ["stats"] = new Dictionary<string, object>
                {
                    ["mean"] = Math.Round(mean, 2),
                    ["std"] = Math.Round(std, 2),
                    ["skew"] = Math.Round(skew, 2),
                    ["min"] = Math.Round(scores[0], 2),
                    ["max"] = Math.Round(scores[^1], 2),
                    ["n"] = n
                }
            };
        }

        return result;
    }

    private static double[] KMeans1D(List<double> sorted, int k, int maxIter = 50)
    {
        var n = sorted.Count;
        var centroids = new double[k];
        for (var i = 0; i < k; i++)
        {
            var idx = (int)((i + 0.5) / k * n);
            centroids[i] = sorted[Math.Min(idx, n - 1)];
        }

        for (var iter = 0; iter < maxIter; iter++)
        {
            var sums = new double[k];
            var counts = new int[k];

            foreach (var val in sorted)
            {
                var best = 0;
                var bestDist = Math.Abs(val - centroids[0]);
                for (var c = 1; c < k; c++)
                {
                    var dist = Math.Abs(val - centroids[c]);
                    if (dist < bestDist) { bestDist = dist; best = c; }
                }
                sums[best] += val;
                counts[best]++;
            }

            var changed = false;
            for (var c = 0; c < k; c++)
            {
                if (counts[c] == 0) continue;
                var newCentroid = sums[c] / counts[c];
                if (Math.Abs(newCentroid - centroids[c]) > 1e-6) changed = true;
                centroids[c] = newCentroid;
            }
            if (!changed) break;
        }

        Array.Sort(centroids);
        return centroids;
    }
}
