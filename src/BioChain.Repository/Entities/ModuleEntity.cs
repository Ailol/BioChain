namespace BioChain.Repository.Entities;

public class ModuleEntity
{
    public int Id { get; set; }
    public Guid? SubjectId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Namespace { get; set; }
    public int? ParentId { get; set; }
    public string? AgentType { get; set; }
    public string? Properties { get; set; }
    public DateTimeOffset CreatedOnUtc { get; set; }

    public SubjectEntity? Subject { get; set; }
    public ModuleEntity? Parent { get; set; }
    public ICollection<ModuleEntity> Children { get; set; } = [];
}
