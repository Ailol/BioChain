using System.Threading.Channels;
using Repository;

namespace Agents;

public record EmbeddingWorkItem(string Person, string Topic, string Explanation);

/// <summary>
/// Background service that processes embedding generation requests from a bounded channel.
/// Replaces fire-and-forget Task.Run with trackable, bounded, error-logged processing.
/// </summary>
public class BackgroundEmbeddingQueue
{
    private readonly Channel<EmbeddingWorkItem> _channel = Channel.CreateBounded<EmbeddingWorkItem>(
        new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest });

    public ChannelReader<EmbeddingWorkItem> Reader => _channel.Reader;

    public void Enqueue(EmbeddingWorkItem item)
    {
        if (!_channel.Writer.TryWrite(item))
            Console.Error.WriteLine($"Embedding queue full, dropped: {item.Person}/{item.Topic}");
    }
}
