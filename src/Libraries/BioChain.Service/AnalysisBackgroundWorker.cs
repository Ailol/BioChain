using Microsoft.Extensions.Hosting;

namespace BioChain.Service;

public class AnalysisBackgroundWorker(
    AnalysisQueueService _queue,
    AnalyzeService _analyzeService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("[AnalysisWorker] Background analysis worker started");

        await foreach (var item in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                Console.WriteLine($"[AnalysisWorker] Processing: {item.PersonName}, " +
                    $"source={item.SourceType}, signals={item.TargetSignals?.Count ?? 0}");

                await _analyzeService.AnalyzeAsync(
                    item.PersonName,
                    item.Text,
                    sourceType: item.SourceType,
                    save: item.Save,
                    targetSignals: item.TargetSignals);

                Console.WriteLine($"[AnalysisWorker] Completed: {item.PersonName}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Log and continue — don't let one failed analysis kill the worker.
                // No retry: the answer is safely persisted, and the next question
                // adds more signal to the profile naturally.
                Console.WriteLine($"[AnalysisWorker] Failed for {item.PersonName}: {ex.Message}");
            }
        }

        Console.WriteLine("[AnalysisWorker] Background analysis worker stopped");
    }
}
