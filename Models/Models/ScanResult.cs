namespace Models;

public partial class PersonalityService
{
    public record ScanResult(string Person, List<Trait> Extracted, List<Trait> Added);
}
