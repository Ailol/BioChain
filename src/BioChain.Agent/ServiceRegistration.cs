using Microsoft.Extensions.DependencyInjection;

namespace BioChain.Agent;

public static class ServiceRegistration
{
    /// <summary>
    /// Register BioChain.Agent services: HttpClient for vLLM, SpacetimeDB service.
    /// </summary>
    public static IServiceCollection AddBioChainAgent(this IServiceCollection services)
    {
        // Plain HttpClient for vLLM — bypasses Aspire's 30s resilience timeout.
        // LLM inference with grammar constraints can take minutes.
        services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromMinutes(10) });
        services.AddSingleton<SpacetimeService>();

        return services;
    }
}
