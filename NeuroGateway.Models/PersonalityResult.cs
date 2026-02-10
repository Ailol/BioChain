namespace NeuroGateway.Models;

public record PersonalityResult(PersonalityProfile? Profile, List<string>? Suggestions = null);
