using System.Text.Json.Serialization;

namespace Models;

/// <summary>
/// Supported conversation file formats for parsing
/// </summary>
public enum ConversationFormat
{
    Unknown,
    PlainText,
    WhatsApp,
    Discord,
    CSV,
    SMSExport,  // "Received from Name on DATE" / "Sent to Name on DATE" format
    Docx,
    Pdf
}

/// <summary>
/// A single message parsed from a conversation
/// </summary>
public record ConversationMessage(
    [property: JsonPropertyName("speaker")] string Speaker,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("timestamp")] DateTime? Timestamp = null,
    [property: JsonPropertyName("isTargetPersonality")] bool IsTargetPersonality = false
);

/// <summary>
/// Trait extracted from conversation before NeuroAgent evaluation
/// </summary>
public record ExtractedTrait(
    [property: JsonPropertyName("topic")] string Topic,
    [property: JsonPropertyName("explanation")] string Explanation,
    [property: JsonPropertyName("speaker")] string Speaker
);

/// <summary>
/// A conversation exchange marked as significant for personality analysis
/// </summary>
public record ImportantConversation(
    [property: JsonPropertyName("startIndex")] int StartIndex,
    [property: JsonPropertyName("endIndex")] int EndIndex,
    [property: JsonPropertyName("messages")] List<ConversationMessage> Messages,
    [property: JsonPropertyName("significanceReason")] string SignificanceReason,
    [property: JsonPropertyName("extractedTraits")] List<ExtractedTrait> ExtractedTraits
);

/// <summary>
/// Request parameters for conversation analysis
/// </summary>
public record ConversationAnalysisRequest(
    string FileContent,
    string TargetPersonalityName,
    string UserName,
    ConversationFormat? FormatHint = null,
    bool AutoAdd = false
);

/// <summary>
/// Individual NeuroAgent decision for a trait
/// </summary>
public record NeuroAgentDecision(
    [property: JsonPropertyName("topic")] string Topic,
    [property: JsonPropertyName("neurotransmitter")] string Neurotransmitter,
    [property: JsonPropertyName("explanation")] string Explanation
);

/// <summary>
/// Result of conversation analysis
/// </summary>
public record ConversationAnalysisResult(
    [property: JsonPropertyName("targetPersonality")] string TargetPersonality,
    [property: JsonPropertyName("userName")] string UserName,
    [property: JsonPropertyName("detectedFormat")] ConversationFormat DetectedFormat,
    [property: JsonPropertyName("totalMessages")] int TotalMessages,
    [property: JsonPropertyName("importantConversationCount")] int ImportantConversationCount,
    [property: JsonPropertyName("significantExchanges")] List<ImportantConversation> SignificantExchanges,
    [property: JsonPropertyName("allExtractedTraits")] List<ExtractedTrait> AllExtractedTraits,
    [property: JsonPropertyName("addedTraits")] List<Trait> AddedTraits,
    [property: JsonPropertyName("neuroDecisions")] List<NeuroAgentDecision> NeuroDecisions
);

/// <summary>
/// Result of document analysis (CV, PDF, DOCX — non-conversation documents)
/// </summary>
public record DocumentAnalysisResult(
    [property: JsonPropertyName("person")] string Person,
    [property: JsonPropertyName("documentType")] string DocumentType,
    [property: JsonPropertyName("extractedTraits")] List<Trait> ExtractedTraits,
    [property: JsonPropertyName("addedTraits")] List<Trait> AddedTraits,
    [property: JsonPropertyName("neuroDecisions")] List<NeuroAgentDecision> NeuroDecisions
);
