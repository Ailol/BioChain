namespace BioChain.Repository.Entities;

// Seed data: each row = one option for one question (flat table, 54 rows total).
public class QuestionnaireItemEntity
{
    public int Id { get; set; }
    public int SortOrder { get; set; }
    public string Scenario { get; set; } = "";
    public string Label { get; set; } = "";
    public string OptionText { get; set; } = "";
    public string PrimaryChemical { get; set; } = "";
    public string? SecondaryChemical { get; set; }
    public bool IsInverted { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Config { get; set; } = "{}";
}

// Runtime: a questionnaire instance sent to / created for a person.
public class QuestionnaireEntity
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public string Token { get; set; } = "";
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Config { get; set; } = "{}";
}

// Runtime: one selected option per question per questionnaire.
public class QuestionnaireAnswerEntity
{
    public int Id { get; set; }
    public Guid QuestionnaireId { get; set; }
    public int ItemId { get; set; }
    public DateTime CreatedAt { get; set; }
}
