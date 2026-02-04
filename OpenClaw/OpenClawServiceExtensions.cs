using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenClaw.Gateway;
using OpenClaw.Skills;

namespace OpenClaw;

/// <summary>
/// Extension methods for registering OpenClaw services with dependency injection.
/// </summary>
public static class OpenClawServiceExtensions
{
    /// <summary>
    /// Add OpenClaw Gateway client and skill service to the service collection.
    /// </summary>
    public static IServiceCollection AddOpenClaw(this IServiceCollection services, Action<OpenClawOptions>? configure = null)
    {
        var options = new OpenClawOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);

        // Register Gateway client
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<OpenClawGatewayClient>>();
            return new OpenClawGatewayClient(logger, options.GatewayUrl);
        });

        // Register HTTP client for MCP calls
        services.AddHttpClient<IMultiAgentSkillHandler, HttpMultiAgentSkillHandler>(client =>
        {
            client.BaseAddress = new Uri(options.McpEndpoint);
            client.Timeout = TimeSpan.FromMinutes(5); // Long timeout for agent discussions
        });

        // Register skill handler
        services.AddSingleton<IMultiAgentSkillHandler>(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(HttpMultiAgentSkillHandler));
            var logger = sp.GetRequiredService<ILogger<HttpMultiAgentSkillHandler>>();
            return new HttpMultiAgentSkillHandler(httpClient, logger, options);
        });

        // Register background service
        services.AddHostedService<MultiAgentSkillService>();

        return services;
    }

    /// <summary>
    /// Add OpenClaw with direct service injection (for when MultiAgentService is available).
    /// </summary>
    public static IServiceCollection AddOpenClawWithDirectServices<TSkillHandler>(
        this IServiceCollection services, 
        Action<OpenClawOptions>? configure = null)
        where TSkillHandler : class, IMultiAgentSkillHandler
    {
        var options = new OpenClawOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);

        // Register Gateway client
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<OpenClawGatewayClient>>();
            return new OpenClawGatewayClient(logger, options.GatewayUrl);
        });

        // Register custom skill handler
        services.AddSingleton<IMultiAgentSkillHandler, TSkillHandler>();

        // Register background service
        services.AddHostedService<MultiAgentSkillService>();

        return services;
    }
}
