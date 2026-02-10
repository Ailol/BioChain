using Microsoft.Extensions.Hosting;
using NeuroGateway.AgentFramework;

namespace NeuroGateway.Service;

/// <summary>
/// Background processor that enriches personality profiles after neurorespond returns.
/// Reads from BackgroundEnrichmentQueue, calls PersonalityService.EnrichFromMessageAsync.
/// </summary>
public class EnrichmentQueueProcessor(BackgroundEnrichmentQueue queue, PersonalityService personalityService)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await personalityService.EnrichFromMessageAsync(item.Person, item.Message);
                Console.Error.WriteLine($"Enrichment complete for {item.Person}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Enrichment error for {item.Person}: {ex.Message}");
            }
        }
    }
}
