# MCP Multi-Agent Group Chat Server

A .NET MCP server that provides multi-agent group chat discussions using local Ollama LLM. Multiple AI agents collaborate in round-robin style to analyze topics from different perspectives.

## Available Tools

| Tool | Description |
|------|-------------|
| `group_chat` | Full 5-agent discussion (Researcher, Creative, Critic, Planner, Synthesizer) |
| `focused_chat` | Custom agent selection for targeted discussions |
| `research_chat` | Quick Researcher + Critic discussion |
| `brainstorm_chat` | Creative + Planner ideation session |
| `critique_chat` | Critic + Researcher analysis |

## Agent Roles

- **Researcher**: Gathers facts, data points, and provides context
- **Creative**: Generates innovative ideas and "what if" scenarios
- **Critic**: Identifies weaknesses, challenges assumptions, evaluates risks
- **Planner**: Creates actionable steps, timelines, and contingencies
- **Synthesizer**: Integrates perspectives and provides key takeaways

## Prerequisites

1. **Ollama** running locally with a model installed:
   ```bash
   # Install Ollama from https://ollama.ai
   ollama pull llama3.2
   ```

2. **.NET 8.0 SDK**

## Quick Setup

### 1. Build the project

```bash
dotnet build -c Release
```

### 2. Configure Claude Desktop

Edit your Claude Desktop config file:

**Windows:** `%APPDATA%\Claude\claude_desktop_config.json`
**Mac:** `~/Library/Application Support/Claude/claude_desktop_config.json`

```json
{
  "mcpServers": {
    "multi-agent": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/path/to/McpAgentServer/McpAgentServer.csproj"],
      "env": {
        "OLLAMA_ENDPOINT": "http://localhost:11434",
        "OLLAMA_MODEL": "llama3.2"
      }
    }
  }
}
```

Or use the compiled exe:

```json
{
  "mcpServers": {
    "multi-agent": {
      "command": "C:/path/to/McpAgentServer/bin/Release/net8.0/McpAgentServer.exe",
      "env": {
        "OLLAMA_ENDPOINT": "http://localhost:11434",
        "OLLAMA_MODEL": "llama3.2"
      }
    }
  }
}
```

### 3. Restart Claude Desktop

Restart the app to load the MCP server.

## Usage Examples

Once connected, ask Claude:

- "Run a group_chat about the future of remote work"
- "Use brainstorm_chat to generate startup ideas for AI education"
- "Run critique_chat on this business plan: [your plan]"
- "Use focused_chat with researcher,planner agents to analyze market entry strategy"

## Project Structure

```
McpAgentServer/
├── Program.cs                  # MCP server setup
├── Agents/
│   └── MultiAgentService.cs    # Group chat orchestration
├── Tools/
│   └── AgentTools.cs           # MCP tool definitions
└── McpAgentServer.csproj       # Project file
```

## Configuration

| Variable | Default | Description |
|----------|---------|-------------|
| `OLLAMA_ENDPOINT` | `http://localhost:11434` | Ollama API endpoint |
| `OLLAMA_MODEL` | `llama3.2` | Model to use for agents |

## Test Locally

```bash
# Ensure Ollama is running
ollama serve

# Run the server (will wait for MCP input on stdin)
dotnet run

# Or test with MCP inspector
npx @modelcontextprotocol/inspector dotnet run
```

## How It Works

The group chat follows a round-robin pattern:

1. User provides a topic
2. Each agent takes turns responding, building on previous contributions
3. Agents see the full conversation history
4. Synthesizer concludes with key takeaways
5. Discussion ends when Synthesizer provides "CONCLUSION:" or max iterations reached
