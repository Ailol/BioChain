namespace Models;

public record ScanResult(string Person, List<Trait> Extracted, List<Trait> Added);
