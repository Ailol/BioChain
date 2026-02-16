using System.Text;
using System.Text.Json;
using NeuroGateway.AnalysisFramework;

namespace NeuroGateway.Calibration.Generation;

public class PromptBuilder
{
    private readonly Dictionary<string, Dictionary<string, CorrelationEntry>> _correlationMatrix;
    private readonly Dictionary<string, object>? _percentileBoundaries;
    private readonly string _systemTemplate;
    private readonly string _workTemplate;
    private readonly string _privateTemplate;

    public PromptBuilder()
    {
        _correlationMatrix = LoadCorrelationMatrix();
        _percentileBoundaries = LoadJsonArtifact<Dictionary<string, object>>("percentile_boundaries.json");
        _systemTemplate = LoadPromptTemplate("chemical_system.txt");
        _workTemplate = LoadPromptTemplate("shadow_work.txt");
        _privateTemplate = LoadPromptTemplate("shadow_private.txt");
    }

    public string BuildSystemPrompt(string chemical)
    {
        var layer = DimensionDefinitions.ChemicalToLayer.TryGetValue(chemical, out var l) ? l : "unknown";
        var correlations = BuildCorrelationBlock(chemical);

        return _systemTemplate
            .Replace("{chemical_name}", chemical)
            .Replace("{layer_name}", layer)
            .Replace("{chemical_correlations}", correlations);
    }

    public string BuildUserPrompt(string chemical, string mode, string? filterDimension = null)
    {
        var template = mode.Equals("private", StringComparison.OrdinalIgnoreCase)
            ? _privateTemplate : _workTemplate;

        var dims = GetDimensionsForChemical(chemical);
        if (filterDimension != null)
            dims = dims.Where(d => d.Name.Equals(filterDimension, StringComparison.OrdinalIgnoreCase)).ToList();

        var sb = new StringBuilder();
        foreach (var dim in dims)
        {
            sb.AppendLine($"=== DIMENSION: {dim.Name} ===");
            sb.AppendLine($"Description: {dim.Description}");
            sb.AppendLine("Percentile context:");
            var boundaries = GetBoundaries(dim.Name);
            if (boundaries != null)
            {
                sb.AppendLine($"  Level 1 = 0-20th percentile (boundary: {boundaries[0]:F2})");
                sb.AppendLine($"  Level 2 = 20-40th percentile (boundary: {boundaries[1]:F2})");
                sb.AppendLine($"  Level 3 = 40-60th percentile (boundary: {boundaries[2]:F2})");
                sb.AppendLine($"  Level 4 = 60-80th percentile (boundary: {boundaries[3]:F2})");
                sb.AppendLine($"  Level 5 = 80-100th percentile");
            }
            else
            {
                sb.AppendLine("  (percentile data not yet available)");
            }
            sb.AppendLine("---");
            sb.AppendLine();
        }

        return template
            .Replace("{chemical_name}", chemical)
            .Replace("{dimension_blocks}", sb.ToString());
    }

    public IReadOnlyList<string> GetAllChemicals() =>
        DimensionDefinitions.ChemicalToLayer.Keys.ToList();

    public List<DimensionDefinitions.DimensionDef> GetDimensionsForChemical(string chemical)
    {
        return DimensionDefinitions.All
            .Where(d => d.ChemicalAffinity.ContainsKey(chemical))
            .ToList();
    }

    private string BuildCorrelationBlock(string chemical)
    {
        if (!_correlationMatrix.TryGetValue(chemical, out var dims))
            return "(no correlation data available)";

        var sb = new StringBuilder();
        foreach (var (dimName, entry) in dims)
            sb.AppendLine($"- {dimName}: strength {entry.Strength:F2}, confidence {entry.Confidence:F1}, source: {entry.Source}");
        return sb.ToString();
    }

    private double[]? GetBoundaries(string dimensionName)
    {
        if (_percentileBoundaries == null) return null;
        if (!_percentileBoundaries.TryGetValue("dimensions", out var dimsObj)) return null;

        if (dimsObj is JsonElement dimsElem && dimsElem.ValueKind == JsonValueKind.Object)
        {
            if (dimsElem.TryGetProperty(dimensionName, out var dimElem) &&
                dimElem.TryGetProperty("quintileBoundaries", out var boundaries) &&
                boundaries.ValueKind == JsonValueKind.Array)
            {
                return boundaries.EnumerateArray().Select(e => e.GetDouble()).ToArray();
            }
        }
        return null;
    }

    private static Dictionary<string, Dictionary<string, CorrelationEntry>> LoadCorrelationMatrix()
    {
        var path = ResolveConfigPath("correlation_matrix.json");
        if (path == null) return new();

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, CorrelationEntry>>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
    }

    private static T? LoadJsonArtifact<T>(string filename) where T : class
    {
        var outputDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "NeuroGateway.Calibration", "Outputs");
        var path = Path.Combine(outputDir, filename);
        if (!File.Exists(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, "Outputs", filename);
            if (!File.Exists(path)) return null;
        }
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private static string LoadPromptTemplate(string filename)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Prompts", filename);
        if (!File.Exists(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "NeuroGateway.Calibration", "Prompts", filename);
        }
        return File.Exists(path) ? File.ReadAllText(path) : $"(template {filename} not found)";
    }

    private static string? ResolveConfigPath(string filename)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Config", filename);
        if (File.Exists(path)) return path;
        path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "NeuroGateway.Calibration", "Config", filename);
        return File.Exists(path) ? path : null;
    }

    public record CorrelationEntry(double Strength, double Confidence, string Source);
}
