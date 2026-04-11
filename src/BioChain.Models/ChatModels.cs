namespace BioChain.Models;

public sealed class ChatResult
{
    public ulong ProgramId { get; set; }
    public string Response { get; set; } = "";
    public int ContextLength { get; set; }
    public List<ToolAction> ToolActions { get; set; } = [];
}

public sealed class ChatTurn
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
}

public sealed class ToolAction
{
    public string Tool { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string Result { get; set; } = "";
}

