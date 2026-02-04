namespace Models;

public partial class PersonalityService
{
    // DTOs
    public record PersonalityResult(PersonalityProfile? Profile, List<string>? Suggestions = null);
}
