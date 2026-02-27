namespace BioChain.Repository.Entities;

public class QuestionnaireItemEntity
{
    public int Id { get; set; }
    public int SortOrder { get; set; }
    public string Scenario { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string OptionText { get; set; } = string.Empty;
    public string? PrimarySignal { get; set; }
    public string? SecondarySignal { get; set; }
    public bool IsInverted { get; set; }
    public string? Data { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<QuestionnaireAnswerEntity> Answers { get; set; } = [];
}
