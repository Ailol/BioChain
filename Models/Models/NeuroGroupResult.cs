namespace Models;

public partial class PersonalityService
{
    public record NeuroGroupResult(string Person, string Topic, List<Trait> Added, string Message);
}
