using static Models.PersonalityService;

namespace Models;

public record FullPersonalityScan(
    string Person,
    List<Trait> Traits,
    List<Interaction> Hormones,
    List<Interaction> Peptides
);

public record Interaction(string Name, float Strength);
