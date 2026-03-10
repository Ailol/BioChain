using BioChain.Agent;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────────────
var vllmEndpoint = builder.Configuration["Vllm:Endpoint"] ?? "http://localhost:8000";
var vllmModel = builder.Configuration["Vllm:Model"] ?? "/models/Qwen3.5-A3B";
var stdbEndpoint = builder.Configuration["SpacetimeDb:Endpoint"] ?? "http://localhost:3000";
var stdbDatabase = builder.Configuration["SpacetimeDb:Database"] ?? "biochain";

// ── Services ──────────────────────────────────────────────────────────────────
var promptDir = Path.Combine(AppContext.BaseDirectory, "Prompts");
if (!Directory.Exists(promptDir))
    promptDir = Path.Combine(Directory.GetCurrentDirectory(), "Prompts");

builder.Services.AddSingleton(new PromptStore(promptDir));

// VLLM client — OpenAI-compatible API
builder.Services.AddHttpClient("vllm", http =>
{
    http.BaseAddress = new Uri(vllmEndpoint);
    http.Timeout = TimeSpan.FromMinutes(5);
});
builder.Services.AddSingleton<ILlmClient>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return new VllmClient(factory.CreateClient("vllm"), vllmModel);
});

// SpacetimeDB module client — HTTP API
builder.Services.AddHttpClient("stdb", http =>
{
    http.BaseAddress = new Uri(stdbEndpoint);
    http.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton<SpacetimeModuleClient>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return new SpacetimeModuleClient(factory.CreateClient("stdb"), stdbDatabase);
});
builder.Services.AddSingleton<IModuleClient>(sp => sp.GetRequiredService<SpacetimeModuleClient>());

builder.Services.AddSingleton<PipelineOrchestrator>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();
app.UseCors();

// ── Endpoints ─────────────────────────────────────────────────────────────────

app.MapGet("/ping", () => "pong");

// Full pipeline: user text → LLM → BNF → parse → store in SpacetimeDB
app.MapPost("/api/infer", async (InferRequest req, PipelineOrchestrator orchestrator, SpacetimeModuleClient stdb) =>
{
    try
    {
        var result = await orchestrator.RunAsync(req.Input, req.ProgramId, PipelineStage.Base);

        // Store raw BNF in SpacetimeDB
        if (result.BnfOutput != null)
        {
            try { await stdb.StoreRawBnfAsync(req.ProgramId, "base", result.BnfOutput); }
            catch (Exception ex)
            {
                result = result with { Errors = [..result.Errors, $"Store failed: {ex.Message}"] };
            }
        }

        return Results.Ok(new InferResponse(result.Success, result.BnfOutput, result.Errors));
    }
    catch (Exception ex)
    {
        return Results.Ok(new InferResponse(false, null, [$"Pipeline error: {ex.Message}"]));
    }
});

// Quick generate: LLM only, no parsing or storage
app.MapPost("/api/generate", async (GenerateRequest req, PromptStore prompts, ILlmClient llm) =>
{
    try
    {
        var systemPrompt = prompts.Get(PipelineStage.Base);
        var bnf = await llm.GenerateAsync(systemPrompt, req.Input, req.Context);
        return Results.Ok(new GenerateResponse(true, bnf, null));
    }
    catch (Exception ex)
    {
        return Results.Ok(new GenerateResponse(false, null, ex.Message));
    }
});

Console.WriteLine($"BioChain Agent starting...");
Console.WriteLine($"  VLLM: {vllmEndpoint} (model: {vllmModel})");
Console.WriteLine($"  SpacetimeDB: {stdbEndpoint}/{stdbDatabase}");
Console.WriteLine($"  Prompts: {promptDir}");

await app.RunAsync();

// ── Request/Response models ───────────────────────────────────────────────────

record InferRequest(string Input, uint ProgramId);
record InferResponse(bool Success, string? Bnf, List<string> Errors);
record GenerateRequest(string Input, string? Context = null);
record GenerateResponse(bool Success, string? Bnf, string? Error);
