namespace BioChain.Repository.Entities;

public class BrainRegionEntity
{
    public int Id { get; set; }
    public string Key { get; set; } = "";            // VTA, NAc, PFC, AMY, HPC, DRN, LC, HYP, PVN
    public string Label { get; set; } = "";
    public string? RegionType { get; set; }          // nucleus, cortical_area, brainstem, gland
    public int? ParentRegionId { get; set; }
    public string Config { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}
