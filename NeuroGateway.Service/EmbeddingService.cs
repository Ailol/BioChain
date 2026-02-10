using NeuroGateway.AgentFramework.Algorithms;
using NeuroGateway.Models;

namespace NeuroGateway.Service;

public class EmbeddingService
{
    public async Task<BackfillResult> BackfillAsync<T>(
        IList<T> items, Func<T, Task<float[]?>> embed, Func<T, string, Task> update, string label)
    {
        int updated = 0, skipped = 0, errors = 0;
        foreach (var item in items)
        {
            try
            {
                var embedding = await embed(item);
                if (embedding == null) { skipped++; continue; }
                await update(item, VectorAlgorithms.ToPostgresVector(embedding));
                updated++;
            }
            catch { errors++; }
        }
        return new BackfillResult(updated, skipped, errors,
            $"{label} complete: {updated} updated, {skipped} skipped, {errors} errors out of {items.Count} items.");
    }
}
