using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Models;
using static Models.PersonalityService;

namespace Agents;

public partial class PersonalityService
{
    private readonly HttpClient _httpClient;
    private readonly string _modelName;
    private readonly string _connectionString;
    private readonly MultiAgentService _multiAgentService;

    public PersonalityService(MultiAgentService multiAgentService)
    {
        var endpoint = Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT") ?? "http://localhost:11434";
        _modelName = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "llama3.2";
        _connectionString = Environment.GetEnvironmentVariable("PERSONALITY_DB")
            ?? "Host=localhost;Database=personality;Username=postgres;Password=postgres";
        _httpClient = new HttpClient { BaseAddress = new Uri(endpoint) };
        _multiAgentService = multiAgentService;
    }

    public async Task<bool> CreatePersonalityAsync(string name)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("INSERT INTO person (name) VALUES (@name) ON CONFLICT DO NOTHING", conn);
        cmd.Parameters.AddWithValue("name", name);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<PersonalityResult> GetPersonalityAsync(string person)
    {
        const string sql = """
            SELECT p.topic, p.explanation, nt.name, pr.name
            FROM personality p
            JOIN person pr ON pr.id = p.person_id
            JOIN neurotransmitter nt ON nt.id = p.neurotransmitter_id
            WHERE LOWER(pr.name) = LOWER(@name) ORDER BY p.topic;
        """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("name", person);

        var traits = new List<Trait>();
        string? matchedName = null;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            traits.Add(new Trait(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
            matchedName ??= reader.GetString(3);
        }

        if (traits.Count > 0)
            return new PersonalityResult(new PersonalityProfile(matchedName!, traits));

        var suggestions = await FindSimilarPersonsAsync(conn, person);
        return new PersonalityResult(null, suggestions.Count > 0 ? suggestions : null);
    }

    public async Task<FullPersonalityScan?> GetFullPersonalityScanAsync(string person)
    {
        const string sql = """
            WITH person_neuro AS (
                SELECT DISTINCT p.neurotransmitter_id, nt.name as neuro_name, pr.name as person_name
                FROM personality p
                JOIN person pr ON pr.id = p.person_id
                JOIN neurotransmitter nt ON nt.id = p.neurotransmitter_id
                WHERE LOWER(pr.name) = LOWER(@name)
            )
            SELECT
                pn.person_name,
                pn.neuro_name,
                i.target_type,
                CASE WHEN i.target_type = 'hormone' THEN h.name ELSE pe.name END as target_name,
                i.strength
            FROM person_neuro pn
            JOIN interaction i ON i.neurotransmitter_id = pn.neurotransmitter_id
            LEFT JOIN hormone h ON i.target_type = 'hormone' AND h.id = i.target_id
            LEFT JOIN peptide pe ON i.target_type = 'peptide' AND pe.id = i.target_id
            ORDER BY pn.neuro_name, i.strength DESC;
        """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("name", person);

        var hormones = new Dictionary<string, float>();
        var peptides = new Dictionary<string, float>();
        string? matchedName = null;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            matchedName ??= reader.GetString(0);
            var targetType = reader.GetString(2);
            var targetName = reader.GetString(3);
            var strength = reader.GetFloat(4);

            var dict = targetType == "hormone" ? hormones : peptides;
            dict[targetName] = Math.Max(dict.GetValueOrDefault(targetName), strength);
        }

        if (matchedName == null) return null;

        var traits = (await GetPersonalityAsync(person)).Profile?.Traits ?? [];

        return new FullPersonalityScan(
            matchedName,
            traits,
            hormones.OrderByDescending(h => h.Value).Select(h => new Interaction(h.Key, h.Value)).ToList(),
            peptides.OrderByDescending(p => p.Value).Select(p => new Interaction(p.Key, p.Value)).ToList()
        );
    }

    private static async Task<List<string>> FindSimilarPersonsAsync(NpgsqlConnection conn, string search)
    {
        const string sql = """
            SELECT name, similarity(LOWER(name), LOWER(@search)) as sim
            FROM person
            WHERE similarity(LOWER(name), LOWER(@search)) > 0.3
            ORDER BY sim DESC
            LIMIT 5;
        """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("search", search);

        var suggestions = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            suggestions.Add(reader.GetString(0));

        return suggestions;
    }

    public async Task<NeuroGroupResult> UpdatePersonalityAsync(string person, string topic, string context)
    {
        var decisions = await _multiAgentService.RunNeuroGroupChatAsync(person, topic, context);

        if (decisions.Count == 0)
            return new NeuroGroupResult(person, topic, [], "No neurotransmitters found this relevant.");

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await new NpgsqlCommand($"INSERT INTO person (name) VALUES ('{person}') ON CONFLICT DO NOTHING", conn).ExecuteNonQueryAsync();

        var added = new List<Trait>();
        foreach (var decision in decisions)
        {
            var sql = """
                INSERT INTO personality (person_id, neurotransmitter_id, topic, explanation)
                SELECT p.id, nt.id, @topic, @expl
                FROM person p, neurotransmitter nt WHERE p.name = @person AND nt.name = @nt
                ON CONFLICT (person_id, neurotransmitter_id, topic)
                DO UPDATE SET explanation = EXCLUDED.explanation;
            """;

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("topic", topic);
            cmd.Parameters.AddWithValue("expl", decision.Explanation);
            cmd.Parameters.AddWithValue("person", person);
            cmd.Parameters.AddWithValue("nt", decision.Neurotransmitter);
            await cmd.ExecuteNonQueryAsync();

            added.Add(new Trait(topic, decision.Explanation, decision.Neurotransmitter));
        }

        return new NeuroGroupResult(person, topic, added, $"{added.Count} neurotransmitter(s) added entries.");
    }

    public async Task<ScanResult> ScanChatAsync(string person, List<OllamaMessage> chat, bool autoAdd = false)
    {
        var text = string.Join("\n", chat.Select(m => $"{m.Role.ToUpper()}: {m.Content}"));
        var prompt = "Extract behavior traits as JSON: [{\"topic\":\"...\",\"explanation\":\"...\"}]. Only clear patterns. Empty [] if none.\n\n" + text;

        var resp = await CallOllamaAsync([new OllamaMessage { Role = "user", Content = prompt }]);
        var extracted = ParseTraits(resp);
        var added = new List<Trait>();

        if (autoAdd)
            foreach (var t in extracted)
            {
                var result = await UpdatePersonalityAsync(person, t.Topic, t.Explanation);
                added.AddRange(result.Added);
            }

        return new ScanResult(person, extracted, added);
    }

    private async Task<string> CallOllamaAsync(List<OllamaMessage> messages)
    {
        var req = new OllamaChatRequest { Model = _modelName, Messages = messages, Stream = false };
        var resp = await _httpClient.PostAsJsonAsync("/api/chat", req);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<OllamaChatResponse>())?.Message?.Content ?? "";
    }

    private static List<Trait> ParseTraits(string json)
    {
        try
        {
            var s = json.IndexOf('['); var e = json.LastIndexOf(']') + 1;
            return s >= 0 && e > s ? JsonSerializer.Deserialize<List<Trait>>(json[s..e]) ?? [] : [];
        }
        catch { return []; }
    }
}
