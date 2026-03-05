namespace BioChain.Kernel.Prompts;

/// <summary>
/// Abstraction for loading prompt text. Implementations may read from files, DB, config, etc.
/// </summary>
public interface IPromptStore
{
    string? Load(string fileName);
    string LoadOrDefault(string fileName, string fallback);
}
