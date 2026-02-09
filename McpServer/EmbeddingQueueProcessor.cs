using Agents;
using Repository;

namespace McpAgentServer;

/// <summary>
/// Hosted service that reads from BackgroundEmbeddingQueue and processes embedding generation.
/// </summary>
public class EmbeddingQueueProcessor : BackgroundService
{
    private readonly BackgroundEmbeddingQueue _queue;
    private readonly EmbeddingService _embeddingService;
    private readonly EmbeddingRepository _embeddingRepo;

    public EmbeddingQueueProcessor(BackgroundEmbeddingQueue queue, EmbeddingService embeddingService, EmbeddingRepository embeddingRepo)
    {
        _queue = queue;
        _embeddingService = embeddingService;
        _embeddingRepo = embeddingRepo;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                var embedding = await _embeddingService.GenerateTraitEmbeddingAsync(item.Topic, item.Explanation);
                if (embedding != null)
                {
                    var vector = EmbeddingService.ToPostgresVector(embedding);
                    await _embeddingRepo.UpdateTraitEmbeddingByContentAsync(item.Person, item.Topic, vector);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Embedding queue error for {item.Person}/{item.Topic}: {ex.Message}");
            }
        }
    }
}
