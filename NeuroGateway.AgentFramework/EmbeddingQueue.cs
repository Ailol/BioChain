using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using NeuroGateway.AgentFramework.Algorithms;
using NeuroGateway.Repository;

namespace NeuroGateway.AgentFramework;

// ===== Embedding Queue (inline embedding backfill) =====

public record EmbeddingWorkItem(string Person, int AnalyzedDataId, string Content);

public class BackgroundEmbeddingQueue
{
    private readonly Channel<EmbeddingWorkItem> _channel = Channel.CreateBounded<EmbeddingWorkItem>(
        new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest });

    public ChannelReader<EmbeddingWorkItem> Reader => _channel.Reader;

    public void Enqueue(EmbeddingWorkItem item)
    {
        if (!_channel.Writer.TryWrite(item))
            Console.Error.WriteLine($"Embedding queue full, dropped: {item.Person}/{item.AnalyzedDataId}");
    }
}

public class EmbeddingQueueProcessor(BackgroundEmbeddingQueue queue, LlmService llm, AnalyzedDataRepository analyzedDataRepo)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                var embedding = await llm.EmbedAsync(item.Content);
                if (embedding != null)
                {
                    var vector = VectorAlgorithms.ToPostgresVector(embedding);
                    await analyzedDataRepo.UpdateEmbeddingAsync(item.AnalyzedDataId, vector);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Embedding queue error for {item.Person}/{item.AnalyzedDataId}: {ex.Message}");
            }
        }
    }
}

// ===== Enrichment Queue (background profile enrichment after neurorespond) =====

public record EnrichmentWorkItem(string Person, string Message);

public class BackgroundEnrichmentQueue
{
    private readonly Channel<EnrichmentWorkItem> _channel = Channel.CreateBounded<EnrichmentWorkItem>(
        new BoundedChannelOptions(50) { FullMode = BoundedChannelFullMode.DropOldest });

    public ChannelReader<EnrichmentWorkItem> Reader => _channel.Reader;

    public void Enqueue(EnrichmentWorkItem item)
    {
        if (!_channel.Writer.TryWrite(item))
            Console.Error.WriteLine($"Enrichment queue full, dropped: {item.Person}");
    }
}
