namespace NeuroGateway.Models;

public record PersonalityProfile(string Person, string? CommunicationStyle, List<AnalyzedEntry> Entries);
