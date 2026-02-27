namespace BioChain.Repository.Entities;

public class QuestionnaireAnswerEntity
{
    public int Id { get; set; }
    public Guid QuestionnaireId { get; set; }
    public int ItemId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public QuestionnaireEntity Questionnaire { get; set; } = null!;
    public QuestionnaireItemEntity Item { get; set; } = null!;
}
