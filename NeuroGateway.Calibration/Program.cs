using System.CommandLine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NeuroGateway.Calibration.Calibration;
using NeuroGateway.Calibration.Etl;
using NeuroGateway.Calibration.Generation;
using NeuroGateway.Models;
using NeuroGateway.Repository;
using NeuroGateway.Service;
using Spectre.Console;

var root = new RootCommand("NeuroGateway Calibration Toolset");

// ── Pipeline 1: ETL ──
var processData = new Command("process-data", "Process psychometric datasets into JSON");
processData.SetAction(async (_, _) =>
{
    using var host = BuildHost();
    var ipip = host.Services.GetRequiredService<IpipNeoProcessor>();
    await ipip.ProcessAsync();
    var onet = host.Services.GetRequiredService<OnetProcessor>();
    await onet.ProcessAsync();
    var supp = host.Services.GetRequiredService<SupplementaryProcessor>();
    await supp.ProcessAsync();
    AnsiConsole.MarkupLine("[green]ETL complete.[/]");
});

// ── Pipeline 2: Generation ──
var generate = new Command("generate", "Generate shadow profile YAML via LLM");
var dimOption = new Option<string?>("--dimension") { Description = "Single dimension to generate" };
var modeOption = new Option<string?>("--mode") { Description = "work or private" };
var dryRunOption = new Option<bool>("--dry-run") { Description = "Show prompts without making API calls" };
var chemicalOption = new Option<string?>("--chemical") { Description = "Single chemical to generate" };
generate.Add(dimOption);
generate.Add(modeOption);
generate.Add(dryRunOption);
generate.Add(chemicalOption);
generate.SetAction(async (parseResult, _) =>
{
    var dim = parseResult.GetValue(dimOption);
    var mode = parseResult.GetValue(modeOption);
    var dryRun = parseResult.GetValue(dryRunOption);
    var chemical = parseResult.GetValue(chemicalOption);

    using var host = BuildHost();
    var gen = host.Services.GetRequiredService<ShadowProfileGenerator>();
    await gen.GenerateAsync(dim, mode, dryRun, chemical);
});

// ── Pipeline 2a: Export prompts for batch API ──
var exportPrompts = new Command("export-prompts", "Export prompts as JSONL for Anthropic batch API");
var exportDimOption = new Option<string?>("--dimension") { Description = "Single dimension to export" };
var exportModeOption = new Option<string?>("--mode") { Description = "work or private" };
var exportChemicalOption = new Option<string?>("--chemical") { Description = "Single chemical to export" };
var exportModelOption = new Option<string>("--model") { Description = "Model ID for batch requests", DefaultValueFactory = _ => "claude-sonnet-4-5-20250929" };
exportPrompts.Add(exportDimOption);
exportPrompts.Add(exportModeOption);
exportPrompts.Add(exportChemicalOption);
exportPrompts.Add(exportModelOption);
exportPrompts.SetAction(async (parseResult, _) =>
{
    var dim = parseResult.GetValue(exportDimOption);
    var mode = parseResult.GetValue(exportModeOption);
    var chemical = parseResult.GetValue(exportChemicalOption);
    var modelId = parseResult.GetValue(exportModelOption);

    using var host = BuildHost();
    var gen = host.Services.GetRequiredService<ShadowProfileGenerator>();
    await gen.ExportPromptsAsync(dim, mode, chemical, modelId!);
});

// ── Pipeline 2c: Assemble raw responses into ShadowProfiles.yaml ──
var assemble = new Command("assemble", "Assemble raw_responses/*.txt into ShadowProfiles.yaml");
assemble.SetAction(async (_, _) =>
{
    using var host = BuildHost();
    var gen = host.Services.GetRequiredService<ShadowProfileGenerator>();
    await gen.AssembleAsync();
});

// ── Pipeline 2b: Validate ──
var validate = new Command("validate", "Validate existing shadow profile YAML");
var validatePathArg = new Argument<string?>("path") { Description = "Path to YAML file to validate", DefaultValueFactory = _ => null };
validate.Add(validatePathArg);
validate.SetAction(async (parseResult, _) =>
{
    var path = parseResult.GetValue(validatePathArg);

    using var host = BuildHost();
    var validator = host.Services.GetRequiredService<YamlValidator>();
    await validator.ValidateAsync(path);
});

// ── Pipeline 3: Calibration (shadow-anchored diagnostics) ──
var calibrate = new Command("calibrate", "Run scoring diagnostics across all persons");
calibrate.SetAction(async (_, _) =>
{
    using var host = BuildHost(requireEmbeddings: true);
    var diag = host.Services.GetRequiredService<SignalAblation>();
    await diag.RunAsync();
});

root.Add(processData);
root.Add(generate);
root.Add(exportPrompts);
root.Add(assemble);
root.Add(validate);
root.Add(calibrate);

return await root.Parse(args).InvokeAsync();

// ── Host builder ─────────────────────────────────────────────────────────────

static IHost BuildHost(bool requireEmbeddings = false)
{
    var builder = Host.CreateDefaultBuilder()
        .ConfigureAppConfiguration((_, config) =>
        {
            var serverDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "NeuroGateway.Server");
            if (Directory.Exists(serverDir))
            {
                config.AddJsonFile(Path.Combine(serverDir, "appsettings.json"), optional: true);
                config.AddJsonFile(Path.Combine(serverDir, "appsettings.Local.json"), optional: true);
            }
            config.AddEnvironmentVariables();
        })
        .ConfigureServices((ctx, services) =>
        {
            var environment = Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "Development";
            var dbEnvVar = environment.Equals("Production", StringComparison.OrdinalIgnoreCase)
                ? "PRODUCTION_DB" : "DEVELOPMENT_DB";

            var db = Environment.GetEnvironmentVariable(dbEnvVar)
                ?? Environment.GetEnvironmentVariable("PERSONALITY_DB")
                ?? ctx.Configuration.GetConnectionString("Personality")
                ?? throw new InvalidOperationException(
                    $"Database connection required. Set {dbEnvVar} env var or ConnectionStrings:Personality in appsettings.");

            AnsiConsole.MarkupLine($"[dim]DB: {db[..Math.Min(60, db.Length)]}...[/]");

            services.AddPooledDbContextFactory<PersonalityDbContext>(options =>
                options.UseNpgsql(db, npgsql => npgsql.UseVector())
                       .UseSnakeCaseNamingConvention());

            // LLM config — mirrors Server's LoadConfig + RegisterAll
            var llm = ctx.Configuration.GetSection("Llm").Get<AgentConfiguration>() ?? new AgentConfiguration();
            services.AddSingleton(llm);

            // IChatClient for generation — uses Orchestrator slot (or AgentReasoning fallback)
            var providerCfg = llm.Orchestrator ?? llm.AgentReasoning ?? llm.AgentAnalyzing;
            if (providerCfg is not null && !string.IsNullOrWhiteSpace(providerCfg.Model)
                && (!string.IsNullOrWhiteSpace(providerCfg.Endpoint) || !string.IsNullOrWhiteSpace(providerCfg.ApiKey)))
            {
                try
                {
                    services.AddSingleton<IChatClient>(CreateChatClient(providerCfg));
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[yellow]LLM not available: {ex.Message}. Generate command will fail.[/]");
                }
            }

            // Embedding generator for calibration diagnostics
            if (requireEmbeddings && llm.Embedding is not null)
            {
                var embedGen = CreateEmbeddingGenerator(llm.Embedding);
                if (embedGen is not null)
                {
                    services.AddSingleton(embedGen);
                    services.AddSingleton<EmbeddingService>();
                    services.AddSingleton<ShadowAnchorService>();
                    services.AddSingleton<DimensionService>();
                    services.AddSingleton<CalibrationService>();
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]Embedding generator not available. Calibration will fail.[/]");
                }
            }

            services.AddSingleton<PersonRepository>();
            services.AddSingleton<PersonalityRepository>();
            services.AddSingleton<AnalyzedDataRepository>();
            services.AddSingleton<ProfileRepository>();
            services.AddSingleton<ProfileService>();

            services.AddTransient<IpipNeoProcessor>();
            services.AddTransient<OnetProcessor>();
            services.AddTransient<SupplementaryProcessor>();
            services.AddTransient(sp => new ShadowProfileGenerator(
                sp.GetRequiredService<PromptBuilder>(),
                sp.GetService<IChatClient>()));
            services.AddTransient<PromptBuilder>();
            services.AddTransient<YamlValidator>();
            services.AddTransient<SignalAblation>();
        });

    return builder.Build();
}

// ── LLM provider factory (same logic as Server/Program.cs) ──────────────────

static Uri EnsureV1Path(Uri uri) =>
    uri.AbsolutePath.Contains("/v1") ? uri : new Uri(uri, "v1/");

static IChatClient CreateChatClient(LlmProviderConfig cfg) =>
    cfg.ResolvedBackend switch
    {
        "Anthropic" => new Anthropic.AnthropicClient
            {
                ApiKey = cfg.ApiKey!,
                HttpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) }
            }.AsIChatClient(cfg.Model),

        "OpenAI" => new OpenAI.OpenAIClient(cfg.ApiKey!)
            .GetChatClient(cfg.Model).AsIChatClient(),

        "Ollama" => new OllamaSharp.OllamaApiClient(
            new HttpClient { BaseAddress = new Uri(cfg.Endpoint!), Timeout = TimeSpan.FromMinutes(10) },
            cfg.Model),

        _ => new OpenAI.OpenAIClient(
                new System.ClientModel.ApiKeyCredential(cfg.ApiKey ?? "unused"),
                new OpenAI.OpenAIClientOptions { Endpoint = EnsureV1Path(new Uri(cfg.Endpoint!)) })
            .GetChatClient(cfg.Model).AsIChatClient()
    };

static IEmbeddingGenerator<string, Embedding<float>>? CreateEmbeddingGenerator(LlmProviderConfig? cfg)
{
    if (cfg is null) return null;
    return cfg.ResolvedBackend switch
    {
        "OpenAI" => new OpenAI.OpenAIClient(cfg.ApiKey!)
            .GetEmbeddingClient(cfg.Model).AsIEmbeddingGenerator(),

        "Ollama" => new OllamaSharp.OllamaApiClient(
            new HttpClient { BaseAddress = new Uri(cfg.Endpoint!), Timeout = TimeSpan.FromMinutes(10) },
            cfg.Model),

        _ => new OpenAI.OpenAIClient(
                new System.ClientModel.ApiKeyCredential(cfg.ApiKey ?? "unused"),
                new OpenAI.OpenAIClientOptions { Endpoint = EnsureV1Path(new Uri(cfg.Endpoint!)) })
            .GetEmbeddingClient(cfg.Model).AsIEmbeddingGenerator()
    };
}
