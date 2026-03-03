using BioChain.Repository.Entities;
using BioChain.Utils.Parsing;

namespace BioChain.Repository.Linking;

/// <summary>
/// Routes parsed DSL lines to the appropriate repository for entity creation.
/// Extracted from AnalyzeService.LinkComponentAsync.
/// </summary>
public interface IComponentLinker
{
    Task LinkAsync(ProtocolEntity protocol, BioChainParser.ParsedLine line,
        Guid subjectId, CancellationToken ct = default);
}
