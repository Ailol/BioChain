using System.Threading.Channels;

namespace NeuroGateway.Service;

// Represents a unit of deferred analysis work for the background worker.
public sealed record AnalysisWorkItem(
    string PersonName,
    string Text,
    string SourceType,
    bool Save,
    IReadOnlySet<string>? TargetSignals);

public class AnalysisQueueService
{
    // Bounded channel: 256 slots provides generous buffer (18 per questionnaire).
    // If the queue backs up, BoundedChannelFullMode.Wait applies back-pressure.
    private readonly Channel<AnalysisWorkItem> _channel =
        Channel.CreateBounded<AnalysisWorkItem>(new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

    public ChannelReader<AnalysisWorkItem> Reader => _channel.Reader;

    public async ValueTask EnqueueAsync(AnalysisWorkItem item, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(item, ct);
    }
}
