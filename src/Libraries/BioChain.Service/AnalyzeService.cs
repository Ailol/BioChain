using BioChain.AgentFramework;
using BioChain.Repository.Entities;
using BioChain.Repository.Linking;
using BioChain.Repository.Repositories;
using BioChain.Utils.Parsing;
using Microsoft.Extensions.AI;

namespace BioChain.Service;

public class AnalyzeService(
    IChatClient engine,
    IStimuliRepository stimuli,
    IProtocolRepository protocols,
    IComponentLinker linker)
{
    private static readonly string? SystemPrompt =
        PromptLoader.Load("SIGNALS_ANALYZER_PROMPT.txt")
        ?? PromptLoader.Load("BIOCHAIN_ANALYZER_PROMPT.txt");

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
            Temperature = 0.7f,
            TopP = 0.8f,
            TopK = 20,
            PresencePenalty = 1.5f,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["chat_template_kwargs"] = new Dictionary<string, object> { ["enable_thinking"] = false },
            },
        };

        ChatResponse response;
        if (SystemPrompt is not null)
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, SystemPrompt),
                new(ChatRole.User, userMessage),
            };
            response = await engine.GetResponseAsync(messages, options, cancellationToken: ct);
        }
        else
        {
            response = await engine.GetResponseAsync(userMessage, options, cancellationToken: ct);
        }

        var raw = StripThinkBlocks(response.Text ?? "");

        // 3. Parse + store: protocol first (to get ID), then link via ComponentLinker
        var lines = BioChainParser.Parse(raw);
        var stored = 0;

        foreach (var line in lines)
        {
            var protocolEntity = new ProtocolEntity
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
                protocolEntity.BindExpr = line.Formula;
            else if (line.Tag is "FAIL")
                protocolEntity.FailCondition = line.Formula;

            var protocol = await protocols.CreateAsync(protocolEntity, ct);
            await linker.LinkAsync(protocol, line, subjectId, ct);
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

public record AnalyzeResult(int StimuliId, int ProtocolsStored, int LinesTotal, string RawOutput);
