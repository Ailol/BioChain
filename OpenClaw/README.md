# OpenClaw Integration for MultiAgentAiMcp

This project provides OpenClaw integration for the MultiAgentAiMcp server, enabling multi-channel access to the Hats, Neuro, and Group Chat agents via WhatsApp, Telegram, Slack, Discord, Voice, and more.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      USER CHANNELS                          │
│   WhatsApp │ Telegram │ Slack │ Discord │ Voice │ WebChat   │
└──────────────────────────┬──────────────────────────────────┘
                           │
                           ▼
              ┌────────────────────────┐
              │     OpenClaw Gateway   │
              │     (control plane)    │
              │   ws://127.0.0.1:18789 │
              └───────────┬────────────┘
                          │ WebSocket
                          ▼
              ┌────────────────────────┐
              │   OpenClaw Project     │
              │   (this library)       │
              │   - Gateway Client     │
              │   - Skill Service      │
              └───────────┬────────────┘
                          │ HTTP/MCP
                          ▼
              ┌────────────────────────┐
              │   MultiAgentAiMcp      │
              │   http://0.0.0.0:13370 │
              │   ├── /mcp endpoint    │
              │   ├── Hats Agents      │
              │   ├── Neuro Agents     │
              │   └── Group Chat       │
              └───────────┬────────────┘
                          │
          ┌───────────────┼───────────────┐
          ▼               ▼               ▼
    ┌──────────┐   ┌──────────┐   ┌──────────┐
    │  Ollama  │   │PostgreSQL│   │  Aspire  │
    │ qwen3-vl │   │personality│   │ metrics  │
    └──────────┘   └──────────┘   └──────────┘
```

## Installation

### 1. Install OpenClaw

Follow the [OpenClaw installation guide](https://github.com/openclaw/openclaw):

```bash
# Install OpenClaw CLI
npm install -g openclaw

# Run the onboarding wizard
openclaw onboard
```

### 2. Install the MultiAgent Skill

Copy the skill configuration to your OpenClaw skills directory:

```bash
# Option A: Install from ClawHub (when published)
openclaw skill install multiagent-mcp

# Option B: Manual installation
cp Config/multiagent-mcp-skill.yaml ~/.openclaw/skills/
```

### 3. Configure the MCP Endpoint

Edit the skill configuration to point to your MCP server:

```yaml
connection:
  endpoint: http://localhost:13370/mcp  # Your MCP server URL
```

## Usage

### From OpenClaw Chat Channels

Once connected, you can invoke the agents from any configured channel:

**WhatsApp/Telegram:**
```
Run a hats discussion about burnout

Analyze my morning routine from a neuro perspective

What's my personality profile?
```

**Slack (with slash commands):**
```
/hats-chat work-life balance
/neuro-chat motivation and procrastination
/personality show
```

**Voice:**
```
"Hey Molty, run a hats chat about my sleep issues"
"What does my personality look like?"
```

### Programmatic Usage

Add the OpenClaw integration to your .NET application:

```csharp
// Program.cs
using OpenClaw;

var builder = WebApplication.CreateBuilder(args);

// Add OpenClaw integration (HTTP mode - calls MCP endpoint)
builder.Services.AddOpenClaw(options =>
{
    options.GatewayUrl = "ws://127.0.0.1:18789";
    options.McpEndpoint = "http://localhost:13370/mcp";
    options.EnableVoice = true;
});

var app = builder.Build();
app.Run();
```

Or with direct service injection (in-process):

```csharp
// When MultiAgentService and PersonalityService are available
builder.Services.AddOpenClawWithDirectServices<
    DirectMultiAgentSkillHandler<MultiAgentService, PersonalityService>>(options =>
{
    options.GatewayUrl = "ws://127.0.0.1:18789";
});
```

## Available Tools

| Tool | Description |
|------|-------------|
| `hats_chat` | 7-agent discussion bridging medicine and systems engineering |
| `neuro_chat` | 7-agent neurotransmitter perspective analysis |
| `group_chat` | Standard 5-agent collaborative discussion |
| `get_personality` | Retrieve personality profile with traits |
| `update_personality` | Submit behavior for agent evaluation |
| `full_personality_scan` | Comprehensive scan with hormones/peptides |
| `create_personality` | Create a new person profile |
| `scan_chat_update_personality` | Analyze chat for patterns |

## Channel Routing

The skill supports automatic routing based on channel type:

| Channel | Default Tool | Use Case |
|---------|--------------|----------|
| Telegram | `neuro_chat` | Quick neurotransmitter insights |
| Slack | `hats_chat` | Team systems discussions |
| WhatsApp | `group_chat` | General multi-agent chat |
| Discord | `group_chat` | Community discussions |
| Voice | `hats_chat` | Hands-free analysis |

## Configuration

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `OPENCLAW_GATEWAY_URL` | `ws://127.0.0.1:18789` | Gateway WebSocket URL |
| `MCP_ENDPOINT` | `http://localhost:13370/mcp` | MCP server endpoint |
| `OPENCLAW_RECONNECT_DELAY` | `5` | Seconds before reconnect attempt |

### OpenClawOptions

```csharp
services.AddOpenClaw(options =>
{
    options.GatewayUrl = "ws://127.0.0.1:18789";
    options.McpEndpoint = "http://localhost:13370/mcp";
    options.ReconnectDelaySeconds = 5;
    options.EnableVoice = true;
    options.EnabledChannels = new List<string> 
    { 
        "whatsapp", "telegram", "slack", "discord", "webchat", "voice" 
    };
});
```

## Security Considerations

1. **Run OpenClaw in Docker sandbox** for isolation
2. **Configure allowed tools** - disable dangerous operations
3. **Enable confirmation** for sensitive actions
4. **Monitor logs** for unusual activity

See [OpenClaw Security Guide](https://github.com/openclaw/openclaw/blob/main/docs/security.md) for details.

## Troubleshooting

### Connection Issues

```bash
# Check if OpenClaw Gateway is running
openclaw status

# Check if MCP server is accessible
curl http://localhost:13370/health

# View Gateway logs
openclaw logs gateway
```

### Skill Not Found

```bash
# List installed skills
openclaw skill list

# Reinstall skill
openclaw skill uninstall multiagent-mcp
openclaw skill install ./Config/multiagent-mcp-skill.yaml
```

## License

MIT
