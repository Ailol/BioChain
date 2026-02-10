namespace NeuroGateway.Models;

public record NeuroGroupResult(string Person, string Content, List<AnalyzedEntry> Added, string Message);
