using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BioChain.Parser;

namespace BioChain.Agent;

/// <summary>
/// IModuleClient implementation using SpacetimeDB HTTP API.
/// Calls reducers and SQL queries against the SpacetimeDB module.
/// </summary>
public class SpacetimeModuleClient : IModuleClient
{
    private readonly HttpClient _http;
    private readonly string _database;

    public SpacetimeModuleClient(HttpClient http, string database = "biochain")
    {
        _http = http;
        _database = database;
    }

    public async Task<uint> CreateProgramAsync(string subjectId, string label, string domains)
    {
        var domainList = domains.Split(',').Select(d => d.Trim()).ToArray();
        await CallReducerAsync("create_program", [label, Opt<string>(null), domainList]);

        // Query back the latest program
        var rows = await SqlAsync($"SELECT * FROM program WHERE name = '{label}'");
        if (rows.Count == 0)
            throw new InvalidOperationException($"Program '{label}' not found after creation");

        var id = rows.Last().GetProperty("id").GetUInt32();
        return id;
    }

    public async Task SetProgramStageAsync(uint programId, byte stage)
    {
        // Store stage as raw_base/raw_plasticity/raw_meta/raw_convergence marker
        // The Rust module doesn't have a dedicated stage field, so this is a no-op
        // Stage tracking is done by which raw_* fields are populated
    }

    public async Task<string?> ReconstructBnfAsync(uint programId)
    {
        var result = await CallReducerAsync("reconstruct", [programId]);
        return result;
    }

    public async Task<List<string>> ExecuteCommandsAsync(uint programId, List<ParsedCommand> commands)
    {
        var errors = new List<string>();

        foreach (var cmd in commands)
        {
            try
            {
                switch (cmd)
                {
                    case SetDomains sd:
                        // Domains are set at program creation, skip
                        break;

                    case InsertNode node:
                        var stateArg = node.State > 0
                            ? Some(new { sym = StateToSymbol(node.State), delta_sign = None(), delta_val = None() })
                            : None();

                        await CallReducerAsync("add_node", [
                            programId,
                            node.Code,
                            node.TypeSub,
                            Opt(string.IsNullOrEmpty(node.Region) ? null : node.Region),
                            $"R{node.Rank}",
                            stateArg,
                            None(), // integ
                            Array.Empty<string>(), // field_ops
                            Array.Empty<object>(), // props
                            node.IsRoot,
                        ]);
                        break;

                    case InsertEdge edge:
                        // Need to resolve source/target refs to IDs
                        // For now, store as raw BNF instead
                        errors.Add($"Edge insertion not yet implemented: {edge.SourceRef}→{edge.TargetRef}");
                        break;

                    default:
                        errors.Add($"Command type not yet implemented: {cmd.GetType().Name}");
                        break;
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{cmd.GetType().Name}: {ex.Message}");
            }
        }

        return errors;
    }

    public async Task EngineTickAsync(uint programId)
    {
        await CallReducerAsync("tick", [programId]);
    }

    /// <summary>
    /// Store raw BNF text for a pipeline layer.
    /// </summary>
    public async Task StoreRawBnfAsync(uint programId, string layer, string bnfText)
    {
        await CallReducerAsync("store_raw_bnf", [programId, layer, bnfText]);
    }

    // ── HTTP helpers ──────────────────────────────────────────────────────────

    private async Task<string> CallReducerAsync(string reducer, object[] args)
    {
        var url = $"/v1/database/{_database}/call/{reducer}";
        var content = new StringContent(
            JsonSerializer.Serialize(args),
            Encoding.UTF8,
            "application/json"
        );

        var res = await _http.PostAsync(url, content);
        if (!res.IsSuccessStatusCode)
        {
            var errorText = await res.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Reducer {reducer} failed ({res.StatusCode}): {errorText}");
        }

        return await res.Content.ReadAsStringAsync();
    }

    private async Task<List<JsonElement>> SqlAsync(string query)
    {
        var url = $"/v1/database/{_database}/sql";
        var content = new StringContent(query, Encoding.UTF8, "text/plain");
        var res = await _http.PostAsync(url, content);

        if (!res.IsSuccessStatusCode)
        {
            var errorText = await res.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"SQL failed ({res.StatusCode}): {errorText}");
        }

        var json = await res.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<JsonElement[]>(json);
        if (data == null || data.Length == 0) return [];

        var table = data[0];
        if (!table.TryGetProperty("rows", out var rows)) return [];

        var result = new List<JsonElement>();
        foreach (var row in rows.EnumerateArray())
        {
            // SpacetimeDB returns positional arrays — decode using schema
            if (table.TryGetProperty("schema", out var schema))
            {
                var elements = schema.GetProperty("elements");
                var obj = new Dictionary<string, JsonElement>();
                int i = 0;
                foreach (var el in elements.EnumerateArray())
                {
                    var name = el.TryGetProperty("name", out var nameEl)
                        ? (nameEl.ValueKind == JsonValueKind.Object
                            ? nameEl.GetProperty("some").GetString()
                            : nameEl.GetString())
                        : $"_{i}";
                    if (i < row.GetArrayLength())
                        obj[name!] = row[i];
                    i++;
                }
                result.Add(JsonSerializer.SerializeToElement(obj));
            }
        }

        return result;
    }

    // ── Option<T> encoding for SpacetimeDB ────────────────────────────────────

    private static object Opt(string? val) =>
        val != null ? new { some = val } : (object)new { none = Array.Empty<object>() };

    private static object Some(object val) => new { some = val };
    private static object None() => new { none = Array.Empty<object>() };

    private static string StateToSymbol(byte state) => state switch
    {
        1 => "↑↑",
        2 => "↑",
        3 => "≈",
        4 => "↓",
        5 => "↓↓",
        6 => "~",
        7 => "⊘",
        8 => "●",
        _ => "≈",
    };
}
