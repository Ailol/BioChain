using Microsoft.Extensions.DependencyInjection;

namespace BioChain.Service;

public static class ServiceRegistration
{
    /// <summary>
    /// Register BioChain.Service layer: LlmClient, PromptStore, orchestrators, facade.
    /// Call after AddBioChainAgent().
    /// </summary>
    public static IServiceCollection AddBioChainService(this IServiceCollection services)
    {
        services.AddSingleton<LlmClient>();
        services.AddSingleton<PromptStore>();
        services.AddSingleton<ToolExecutor>();
        services.AddSingleton<PipelineOrchestrator>();
        services.AddSingleton<ChatOrchestrator>();
        services.AddSingleton<BioChainService>();

        return services;
    }
}
