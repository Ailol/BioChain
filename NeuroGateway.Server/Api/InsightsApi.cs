using NeuroGateway.Service;

namespace NeuroGateway.Server.Api;

public static class InsightsApi
{
    public static RouteGroupBuilder MapInsightsApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/insights").WithTags("Insights");

        // Full dashboard: profile + forecast + prescriptions + health indicators
        group.MapGet("/{person}/dashboard", async (string person, ProfileAnalysisService svc) =>
        {
            var dashboard = await svc.GetDashboardAsync(person);
            return Results.Ok(dashboard);
        });

        // Where is your chemistry heading? Per-chemical trends + cascade alerts
        group.MapGet("/{person}/forecast", async (string person, ProfileAnalysisService svc) =>
        {
            var forecast = await svc.GetForecastAsync(person);
            return Results.Ok(forecast);
        });

        // Exercise prescriptions based on current chemical deficits
        group.MapGet("/{person}/prescriptions", async (string person, ProfileAnalysisService svc) =>
        {
            var prescriptions = await svc.GetPrescriptionsAsync(person);
            return Results.Ok(prescriptions);
        });

        // Burnout risk (cortisol:DHEA), growth window (BDNF), overtraining
        group.MapGet("/{person}/health", async (string person, ProfileAnalysisService svc) =>
        {
            var health = await svc.GetHealthIndicatorsAsync(person);
            return Results.Ok(health);
        });

        // Historical: where has your chemistry BEEN over the last N days
        group.MapGet("/{person}/trajectory", async (string person, int? period, ProfileAnalysisService svc) =>
        {
            var trajectory = await svc.GetTrajectoryAsync(person, period ?? 90);
            return Results.Ok(trajectory);
        });

        // Key signals: top 3-5 most significant signals, fully display-ready
        group.MapGet("/{person}/key-signals", async (string person, ProfileAnalysisService svc) =>
        {
            var result = await svc.GetKeySignalsAsync(person);
            return Results.Ok(result);
        });

        // AI-generated strengths and challenges with receptor-level detail
        group.MapGet("/{person}/strengths-challenges", async (string person, ProfileAnalysisService svc) =>
        {
            var result = await svc.GetStrengthsChallengesAsync(person);
            return Results.Ok(result);
        });

        // Cross-profile: strength × challenge interactions with embedding similarity + LLM suggestions
        group.MapGet("/{person}/cross-profile", async (string person, ProfileAnalysisService svc) =>
        {
            var result = await svc.GetCrossProfileAsync(person);
            return Results.Ok(result);
        });

        // Quick mood check-in: "how do you feel?" → 27 agents → new observations
        group.MapPost("/{person}/checkin", async (string person, CheckInRequest req, ProfileAnalysisService svc) =>
        {
            var result = await svc.ProcessMoodCheckInAsync(person, req.Text);
            return Results.Ok(result);
        });

        return group;
    }
}

public record CheckInRequest(string Text);
