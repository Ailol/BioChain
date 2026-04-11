using BioChain.Agent;
using BioChain.Service;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Config sections
builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection(LlmOptions.Section));
builder.Services.Configure<SpacetimeOptions>(builder.Configuration.GetSection(SpacetimeOptions.Section));

// Agent layer (SpacetimeDB + HttpClient)
builder.Services.AddBioChainAgent();

// Service layer (LLM client, orchestrators, facade)
builder.Services.AddBioChainService();

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // SpacetimeDB generated types use public fields, not properties
        o.JsonSerializerOptions.IncludeFields = true;
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddOpenApi();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Connect to SpacetimeDB on startup
var stdb = app.Services.GetRequiredService<SpacetimeService>();
await stdb.ConnectAsync();

app.MapDefaultEndpoints();
app.UseCors();
app.UseDeveloperExceptionPage();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();
