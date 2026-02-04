namespace Models;

public partial class PersonalityService
{
    public record PersonalityProfile(string Person, List<Trait> Traits);
}
