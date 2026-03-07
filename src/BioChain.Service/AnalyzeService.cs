using BioChain.Kernel.Prompts;
using BioChain.Repository.Entities;
using BioChain.Repository.Linking;
using BioChain.Repository.Repositories;
using BioChain.Utils.Parsing;
using Microsoft.Extensions.AI;

namespace BioChain.Service;

public class AnalyzeService(
    IChatClient engine,
    LlmSemaphore llmSemaphore,
    IPromptStore prompts,
    IStimuliRepository stimuli,
    IAnalysisRepository analyses,
    IComponentLinker linker)
{
    private readonly string? SystemPrompt =
        prompts.Load("BIOCHAIN_ANALYZER_PROMPT.txt")
        ?? prompts.Load("SIGNALS_ANALYZER_PROMPT.txt");

    private static readonly Dictionary<string, string> Preambles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chat"] = "Analyze this conversation for neurochemical cascades:",
        ["cv"] = "Analyze this CV/resume for neurochemical personality patterns:",
        ["notes"] = "Analyze these clinical notes:",
        ["journal"] = "Analyze this journal entry for neurochemical patterns:",
        ["psych"] = "Analyze this psychological assessment:",
        ["document_by"] = "Analyze this document written by the subject:",
        ["document_about"] = "Analyze this document about the subject:",
        ["observation"] = "Analyze this observation:",
    };

    public async Task<AnalyzeResult> AnalyzeAsync(Guid subjectId, string text, string kind,
        CancellationToken ct = default)
    {
        // 1. Log raw input
        var entry = await stimuli.CreateAsync(new StimuliEntity
        {
            SubjectId = subjectId,
            Kind = kind,
            SourceText = text,
        }, ct);

        // 2. Call biochain-engine
        var userMessage = $"{Preambles.GetValueOrDefault(kind, "Analyze this text:")}\n\n{text}";
        var options = new ChatOptions
        {
            MaxOutputTokens = 4096,
            Temperature = 0.3f,
            TopP = 0.8f,
            TopK = 20,
            PresencePenalty = 0.0f,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["chat_template_kwargs"] = new Dictionary<string, object> { ["enable_thinking"] = false },
            },
        };

        var response = await llmSemaphore.RunAsync(async () =>
        {
            if (SystemPrompt is not null)
            {
                var messages = new List<ChatMessage>
                {
                    new(ChatRole.System, SystemPrompt),
                    new(ChatRole.User, userMessage),
                };
                return await engine.GetResponseAsync(messages, options, cancellationToken: ct);
            }
            return await engine.GetResponseAsync(userMessage, options, cancellationToken: ct);
        }, ct);

        var raw = StripThinkBlocks(response.Text ?? "");

        // 3. Parse + store: analysis first (to get ID), then link via ComponentLinker
        var lines = BioChainParser.Parse(raw);
        var stored = 0;

        foreach (var line in lines)
        {
            var analysisEntity = new AnalysisEntity
            {
                SubjectId = subjectId,
                Tag = line.Tag,
                Formula = line.Formula,
                Status = line.Status,
                Phase = line.Phase,
                Seq = stored,
                StimuliId = entry.Id,
            };

            if (line.Tag is "BIND")
                analysisEntity.BindExpr = line.Formula;
            else if (line.Tag is "FAIL")
                analysisEntity.FailCondition = line.Formula;

            var analysis = await analyses.CreateAsync(analysisEntity, ct);
            await linker.LinkAsync(analysis, line, subjectId, ct);
            stored++;
        }

        // 4. Mark analyzed
        await stimuli.MarkAnalyzedAsync(entry.Id, ct);

        return new AnalyzeResult(entry.Id, stored, lines.Count, raw);
    }

    private static string StripThinkBlocks(string raw)
    {
        if (!raw.Contains("</think>")) return raw;
        while (raw.Contains("<think>") && raw.Contains("</think>"))
        {
            var start = raw.IndexOf("<think>", StringComparison.Ordinal);
            var end = raw.IndexOf("</think>", StringComparison.Ordinal) + "</think>".Length;
            if (start >= 0 && end > start)
                raw = (raw[..start] + raw[end..]).TrimStart();
            else
                break;
        }
        return raw.StartsWith("</think>") ? raw["</think>".Length..].TrimStart() : raw;
    }
}

public record AnalyzeResult(int StimuliId, int AnalysesStored, int LinesTotal, string RawOutput);
