using BioChain.Repository.Entities;
using BioChain.Repository.Repositories;
using BioChain.Utils.Parsing;
using Microsoft.Extensions.AI;

namespace BioChain.Service;

public class AnalyzeService(
    IChatClient engine,
    IDataRepository data,
    ISignalRepository signals,
    IReceptorRepository receptors,
    IGateRepository gates,
    ILimiterRepository limiters,
    ITransporterRepository transporters,
    IInterfaceRepository interfaces,
    IProtocolRepository protocols)
{
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

    public async Task<AnalyzeResult> AnalyzeAsync(Guid personId, string text, string kind,
        CancellationToken ct = default)
    {
        // 1. Log raw input
        var entry = await data.CreateAsync(new DataEntity
        {
            PersonId = personId,
            Kind = kind,
            SourceText = text,
        }, ct);

        // 2. Call biochain-engine
        var prompt = BuildPrompt(text, kind);
        var response = await engine.GetResponseAsync(prompt, cancellationToken: ct);
        var raw = response.Text ?? "";

        // 3. Parse + store protocols
        var lines = BioChainParser.Parse(raw);
        var stored = 0;

        foreach (var line in lines)
        {
            var protocol = new ProtocolEntity
            {
                PersonId = personId,
                Tag = line.Tag,
                Formula = line.Formula,
                Status = line.Status,
                Phase = line.Phase,
                DataId = entry.Id,
            };

            await LinkComponentAsync(protocol, line, personId, ct);
            await protocols.CreateAsync(protocol, ct);
            stored++;
        }

        // 4. Mark analyzed
        await data.MarkAnalyzedAsync(entry.Id, ct);

        return new AnalyzeResult(entry.Id, stored, lines.Count, raw);
    }

    private async Task LinkComponentAsync(ProtocolEntity protocol, BioChainParser.ParsedLine line,
        Guid personId, CancellationToken ct)
    {
        switch (line.Tag)
        {
            case "SIGNAL":
            case "STATE":
            {
                var sig = BioChainParser.ExtractSignal(line.Formula);
                if (sig is null) break;
                var entity = await signals.UpsertAsync(new SignalEntity
                {
                    PersonId = personId,
                    Type = sig.Value.Type,
                    Code = sig.Value.Code,
                    State = sig.Value.State ?? "\u2248", // ≈
                    Region = sig.Value.Region,
                }, ct);
                protocol.SignalSourceId = entity.Id;
                break;
            }

            case "RECEPTOR":
            {
                var rec = BioChainParser.ExtractReceptor(line.Formula);
                if (rec is null) break;
                var parent = await signals.GetByCodeAsync(personId, rec.Value.SignalCode, ct: ct);
                if (parent is null) break; // skip if parent signal not found
                var entity = await receptors.UpsertAsync(new ReceptorEntity
                {
                    PersonId = personId,
                    SignalId = parent.Id,
                    Code = rec.Value.Code,
                    State = rec.Value.State ?? "active",
                    Subtype = rec.Value.Subtype,
                }, ct);
                protocol.ReceptorId = entity.Id;
                protocol.SignalSourceId = parent.Id;
                break;
            }

            case "GATE":
            {
                var gate = BioChainParser.ExtractGate(line.Formula);
                if (gate is null) break;
                var entity = await gates.UpsertAsync(new GateEntity
                {
                    PersonId = personId,
                    Code = gate.Value.Expression,
                    Type = gate.Value.Type,
                }, ct);
                protocol.GateId = entity.Id;
                break;
            }

            case "LIMITER":
            {
                var lim = BioChainParser.ExtractLimiter(line.Formula);
                if (lim is null) break;
                var entity = await limiters.UpsertAsync(new LimiterEntity
                {
                    PersonId = personId,
                    Code = lim.Value.Code,
                    Activity = lim.Value.Activity ?? "\u2248",
                    RateLimiting = lim.Value.RateLimiting,
                    Reaction = lim.Value.Reaction,
                }, ct);
                protocol.LimiterId = entity.Id;
                break;
            }

            case "TRANSPORT":
            {
                var tr = BioChainParser.ExtractTransporter(line.Formula);
                if (tr is null) break;
                var signalCode = BioChainParser.MapTransporterToSignal(tr.Value.Code);
                var parent = signalCode is not null
                    ? await signals.GetByCodeAsync(personId, signalCode, ct: ct)
                    : null;
                if (parent is null) break; // skip if parent signal not found
                var entity = await transporters.UpsertAsync(new TransporterEntity
                {
                    PersonId = personId,
                    SignalId = parent.Id,
                    Code = tr.Value.Code,
                    State = tr.Value.State ?? "active",
                    Clearance = tr.Value.Clearance ?? "\u2248",
                }, ct);
                protocol.TransporterId = entity.Id;
                break;
            }

            case "INTERFACE":
            {
                var iface = BioChainParser.ExtractInterface(line.Formula);
                if (iface is null) break;
                var entity = await interfaces.UpsertAsync(new InterfaceEntity
                {
                    PersonId = personId,
                    Code = $"{iface.Value.Source}\u2192{iface.Value.Target}",
                    SourceRegion = iface.Value.Source,
                    TargetRegion = iface.Value.Target,
                    Pathway = iface.Value.Pathway,
                }, ct);
                protocol.InterfaceId = entity.Id;
                break;
            }

            case "FORMULA":
            case "FEEDBACK":
            case "DEF":
            {
                var (src, tgt) = BioChainParser.ExtractFormulaSignalRefs(line.Formula);
                if (src is not null)
                {
                    var s = await signals.GetByCodeAsync(personId, src.Value.Code, src.Value.Region, ct);
                    if (s is not null) protocol.SignalSourceId = s.Id;
                }
                if (tgt is not null && tgt != src)
                {
                    var t = await signals.GetByCodeAsync(personId, tgt.Value.Code, tgt.Value.Region, ct);
                    if (t is not null) protocol.SignalTargetId = t.Id;
                }
                break;
            }

            case "DYSREG":
                await data.CreateAsync(new DataEntity
                    { PersonId = personId, Kind = "inferred", SourceText = line.Formula, Analyzed = true }, ct);
                break;

            case "HYPOTHESIS":
                await data.CreateAsync(new DataEntity
                    { PersonId = personId, Kind = "hypothesis", SourceText = line.Formula, Analyzed = true }, ct);
                break;

            case "PREDICTION":
                await data.CreateAsync(new DataEntity
                    { PersonId = personId, Kind = "prediction", SourceText = line.Formula, Analyzed = true }, ct);
                break;

            case "INTERVENTION":
                await data.CreateAsync(new DataEntity
                    { PersonId = personId, Kind = "clinical", SourceText = line.Formula, Analyzed = true }, ct);
                break;
        }
    }

    private static string BuildPrompt(string text, string kind)
    {
        var preamble = Preambles.GetValueOrDefault(kind, "Analyze this text:");
        return $"{preamble}\n\n{text}";
    }
}

public record AnalyzeResult(int DataId, int ProtocolsStored, int LinesTotal, string RawOutput);
