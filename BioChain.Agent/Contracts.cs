using BioChain.Parser;

namespace BioChain.Agent;

/// <summary>
/// Abstraction over the LLM provider. Claude API, local Qwen via SGLang, etc.
/// The Agent is the only project that knows about LLMs.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Send system prompt + user input (+ optional context) to the LLM.
    /// Returns raw BNF text output.
    /// </summary>
    Task<string> GenerateAsync(string systemPrompt, string userInput, string? existingBnf = null);
}

/// <summary>
/// Abstraction over SpacetimeDB Module reducer calls.
/// Agent calls these to write parsed commands into the Module.
/// </summary>
public interface IModuleClient
{
    Task<uint> CreateProgramAsync(string subjectId, string label, string domains);
    Task SetProgramStageAsync(uint programId, byte stage);
    Task<string?> ReconstructBnfAsync(uint programId);
    Task<List<string>> ExecuteCommandsAsync(uint programId, List<ParsedCommand> commands);
    Task EngineTickAsync(uint programId);
}
