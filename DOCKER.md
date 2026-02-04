# MultiAgentAiMcp Docker Deployment

## Quick Start

### 1. Copy environment file
```bash
cp .env.example .env
# Edit .env if needed (change model, passwords, etc.)
```

### 2. Start services
```bash
# Build and start
docker-compose up -d --build

# View logs
docker-compose logs -f mcpserver
```

### 3. Verify it's running
```bash
# Health check
curl http://localhost:13370/health

# Test with mcporter
mcporter list
mcporter call multiagent.hats_chat topic="hello docker"
```

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  Docker Network: multiagent-network                         │
│                                                             │
│  ┌─────────────────┐      ┌─────────────────┐              │
│  │   mcpserver     │      │    postgres     │              │
│  │   :13370        │─────▶│    :5432        │              │
│  └────────┬────────┘      └─────────────────┘              │
│           │                                                 │
└───────────┼─────────────────────────────────────────────────┘
            │ host.docker.internal
            ▼
    ┌───────────────┐
    │    Ollama     │  (running on host)
    │   :11434      │
    └───────────────┘
```

## Configuration

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `OLLAMA_ENDPOINT` | `http://host.docker.internal:11434` | Ollama API URL |
| `OLLAMA_MODEL` | `qwen3-vl:30b-a3b-instruct-q8_0` | Model to use |
| `POSTGRES_PASSWORD` | `postgres` | PostgreSQL password |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Environment mode |

### Using Different Models

Edit `.env`:
```bash
OLLAMA_MODEL=qwen3-next:80b-a3b-instruct-q4_K_M
```

### Using Dockerized Ollama (with GPU)

Uncomment the `ollama` service in `docker-compose.yml`:
```yaml
ollama:
  image: ollama/ollama:latest
  ...
```

Then update the endpoint:
```bash
OLLAMA_ENDPOINT=http://ollama:11434
```

Pull your model:
```bash
docker exec -it multiagent-ollama ollama pull qwen3-vl:30b-a3b-instruct-q8_0
```

## Development vs Production

### Local Development (no Docker)
```bash
# Run with Aspire (recommended)
dotnet run --project MultiAgentAiMcp.AppHost

# Or direct with environment variables
$env:OLLAMA_MODEL = "qwen3-vl:30b-a3b-instruct-q8_0"
$env:OLLAMA_ENDPOINT = "http://localhost:11434"
dotnet run --project McpServer
```

### Docker Production
```bash
docker-compose up -d
```

### Hybrid (Docker DB, Local Server)
```bash
# Start only postgres
docker-compose up -d postgres

# Run server locally
$env:ConnectionStrings__PersonalityDb = "Host=localhost;Port=5433;Database=personality;Username=postgres;Password=postgres"
dotnet run --project McpServer
```

## Commands

```bash
# Start all services
docker-compose up -d

# Rebuild after code changes
docker-compose up -d --build

# View logs
docker-compose logs -f mcpserver
docker-compose logs -f postgres

# Stop services
docker-compose down

# Stop and remove volumes (WARNING: deletes data)
docker-compose down -v

# Restart specific service
docker-compose restart mcpserver

# Shell into container
docker exec -it multiagent-mcp /bin/bash

# Check health
docker-compose ps
curl http://localhost:13370/health
```

## Connecting mcporter to Docker

mcporter automatically connects via `http://localhost:13370/mcp` - no config change needed!

```bash
mcporter list
# Should show: multiagent (8 tools)

mcporter call multiagent.hats_chat topic="running in docker"
```

## Connecting OpenClaw

Same as before - the endpoint is still `http://localhost:13370/mcp`:

```bash
# Verify mcporter config
cat ~/.mcporter/mcporter.json

# Test from WhatsApp
# "ask the hats about docker containers"
```

## Troubleshooting

### Container won't start
```bash
docker-compose logs mcpserver
```

### Can't connect to Ollama
```bash
# Test from inside container
docker exec -it multiagent-mcp curl http://host.docker.internal:11434/api/tags

# On Linux, you might need:
# Add to docker-compose.yml under mcpserver:
#   extra_hosts:
#     - "host.docker.internal:172.17.0.1"
```

### Database connection issues
```bash
# Check postgres is healthy
docker-compose ps

# Connect to postgres
docker exec -it multiagent-postgres psql -U postgres -d personality
```

### Permission denied on Linux
```bash
# Ensure your user is in docker group
sudo usermod -aG docker $USER
# Then log out and back in
```

## Production Deployment

For production, add:

1. **Reverse proxy** (nginx/traefik) with HTTPS
2. **Strong passwords** in `.env`
3. **Persistent volume backups**
4. **Resource limits** in docker-compose.yml:
   ```yaml
   deploy:
     resources:
       limits:
         memory: 2G
         cpus: '2'
   ```

## File Structure

```
MultiAgentAiMcp/
├── Dockerfile           # MCP Server image
├── docker-compose.yml   # Full stack definition
├── .dockerignore        # Build exclusions
├── .env.example         # Environment template
├── .env                 # Your local config (git-ignored)
├── McpServer/           # MCP Server code
├── Agents/              # Agent implementations
└── OpenClaw/            # OpenClaw integration
```
