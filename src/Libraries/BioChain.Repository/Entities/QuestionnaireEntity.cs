namespace BioChain.Repository.Entities;

public class QuestionnaireEntity
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Data { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public PersonEntity Person { get; set; } = null!;
    public ICollection<QuestionnaireAnswerEntity> Answers { get; set; } = [];
}
