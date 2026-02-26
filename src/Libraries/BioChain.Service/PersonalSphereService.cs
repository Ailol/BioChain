using System.Text;
using System.Text.Json;
using BioChain.AgentFramework;
using BioChain.AnalysisFramework;
using BioChain.Repository;

namespace BioChain.Service;

/// <summary>
/// Generates personal insight data using the layer agents LLM (3+1 neurochat agents).
/// Unlike BioSphere (which aggregates raw data), PersonalSphere asks the layer agents
/// to produce human-readable insights, patterns, leverage points, and strengths.
/// </summary>
public class PersonalSphereService(
    PersonService personService,
    NeuroService neuroService,
    ObservationRepository observationRepo,
    DimensionService dimensionService,
    ChatClient orchestratorClient)
{
    private const string InsightSystemPrompt = """
        You are a personal insight generator. Given a person's biochemical profile data,
        generate structured personal insights in JSON format.

        Return a JSON object with these fields:
        {
          "coreInsights": [
            {
              "id": "unique-id",
              "title": "Short title",
              "body": "2-3 sentences explaining this insight",
              "why": "Why this matters",
              "formulas": ["formula1", "formula2"],
              "signals": {"signal_key": percentage_number},
              "domain": "cognitive|emotional|social|physical"
            }
          ],
          "deepPatterns": [
            {
              "title": "Pattern name",
              "body": "Description",
              "formula": "biochemical formula",
              "icon": "emoji"
            }
          ],
          "leveragePoints": [
            {
              "rank": 1,
              "title": "Leverage point",
              "description": "What to do",
              "impact": 85,
              "feasibility": 70,
              "signals": ["signal_key"],
              "color": "#hex"
            }
          ],
          "strengths": [
            {
              "title": "Strength name",
              "detail": "Description",
              "signal": "signal_key",
              "color": "#hex"
            }
          ],
          "systemRadar": [
            { "system": "System Name", "healthy": 80, "current": 55 }
          ],
          "energyCurve": [
            { "hour": 6, "healthy": 30, "current": 20 },
            { "hour": 8, "healthy": 60, "current": 40 }
          ]
        }

        Generate 3-5 core insights, 2-4 deep patterns, 3-5 leverage points, 2-4 strengths,
        5-8 system radar entries, and 10-18 energy curve points (hours 5-23).

        Base everything on the biochemical data provided. Be specific about which signals
        drive each insight. Use domain-appropriate language.

        CRITICAL: Return ONLY valid JSON. No markdown, no code fences, no commentary.
        """;

    public async Task<PersonalSphereResponse?> GetInsightsAsync(string person)
    {
        var personId = await personService.FindAsync(person);
        if (personId is null) return null;

        // Gather profile data to feed to the LLM
        var signalCounts = await observationRepo.GetSignalCountsAsync(person);
        var dimensions = await dimensionService.ScoreAsync(person, ScoringMode.Private);

        if (signalCounts.Count == 0)
            return new PersonalSphereResponse(person, [], [], [], [], [], []);

        // Build context for the LLM
        var sb = new StringBuilder();
        sb.AppendLine($"Person: {person}");
        sb.AppendLine();
        sb.AppendLine("Signal Profile (signal → observation count):");
        foreach (var (signal, count) in signalCounts.OrderByDescending(s => s.Count).Take(15))
            sb.AppendLine($"  {signal}: {count}");

        sb.AppendLine();
        sb.AppendLine("Dimension Scores:");
        foreach (var dim in dimensions.Take(12))
        {
            sb.AppendLine($"  {dim.Name} ({dim.Section}/{dim.Category}): {dim.Score:F0} " +
                $"[confidence={dim.Confidence:F2}, consistency={dim.Consistency:F2}]");
            if (dim.Posterior is not null)
                sb.AppendLine($"    posterior: MAP={dim.Posterior.MapLevel:F1}, mean={dim.Posterior.MeanLevel:F1}, confidence={dim.Posterior.Confidence:F2} ({dim.Posterior.Interpretation})");
            if (dim.Circuit is not null)
                sb.AppendLine($"    circuit: {dim.Circuit.Pattern} (coherence={dim.Circuit.CoherenceScore:F2})");
        }

        try
        {
            var rawJson = await orchestratorClient.SendAsync(InsightSystemPrompt, sb.ToString());

            // Strip markdown fences if present
            rawJson = rawJson.Trim();
            if (rawJson.StartsWith("```"))
            {
                var firstNewline = rawJson.IndexOf('\n');
                if (firstNewline > 0) rawJson = rawJson[(firstNewline + 1)..];
                if (rawJson.EndsWith("```")) rawJson = rawJson[..^3];
                rawJson = rawJson.Trim();
            }

            var parsed = JsonSerializer.Deserialize<PersonalSphereRawResponse>(rawJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (parsed is null)
                return new PersonalSphereResponse(person, [], [], [], [], [], []);

            return new PersonalSphereResponse(
                person,
                parsed.CoreInsights ?? [],
                parsed.DeepPatterns ?? [],
                parsed.LeveragePoints ?? [],
                parsed.Strengths ?? [],
                parsed.SystemRadar ?? [],
                parsed.EnergyCurve ?? []
            );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[PersonalSphere] LLM error for '{person}': {ex.Message}");
            return new PersonalSphereResponse(person, [], [], [], [], [], []);
        }
    }
}

// ── Internal deserialization model ──
file class PersonalSphereRawResponse
{
    public List<PersonalSphereInsight>? CoreInsights { get; set; }
    public List<PersonalSpherePattern>? DeepPatterns { get; set; }
    public List<PersonalSphereLeveragePoint>? LeveragePoints { get; set; }
    public List<PersonalSphereStrength>? Strengths { get; set; }
    public List<PersonalSphereSystemRadar>? SystemRadar { get; set; }
    public List<PersonalSphereEnergyCurve>? EnergyCurve { get; set; }
}

// ── DTOs ──

public record PersonalSphereResponse(
    string Person,
    List<PersonalSphereInsight> CoreInsights,
    List<PersonalSpherePattern> DeepPatterns,
    List<PersonalSphereLeveragePoint> LeveragePoints,
    List<PersonalSphereStrength> Strengths,
    List<PersonalSphereSystemRadar> SystemRadar,
    List<PersonalSphereEnergyCurve> EnergyCurve);

public record PersonalSphereInsight(
    string Id, string Title, string Body, string Why,
    List<string> Formulas, Dictionary<string, int> Signals,
    string Color, string ColorDim, string ColorGlow, string Domain);

public record PersonalSpherePattern(
    string Title, string Body, string Formula, string Icon);

public record PersonalSphereLeveragePoint(
    int Rank, string Title, string Description,
    int Impact, int Feasibility, List<string> Signals, string Color);

public record PersonalSphereStrength(
    string Title, string Detail, string Signal, string Color);

public record PersonalSphereSystemRadar(
    string System, int Healthy, int Current);

public record PersonalSphereEnergyCurve(
    int Hour, int Healthy, int Current);
