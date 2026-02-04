# MultiAgentAiMcp - MCP Server
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY *.sln ./
COPY McpServer/*.csproj ./McpServer/
COPY Agents/*.csproj ./Agents/
COPY OpenClaw/*.csproj ./OpenClaw/

# Restore dependencies
RUN dotnet restore McpServer/McpServer.csproj

# Copy everything else
COPY . .

# Build release
WORKDIR /src/McpServer
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install curl for healthchecks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Expose MCP port
EXPOSE 13370

# Environment variables (can be overridden)
ENV ASPNETCORE_URLS=http://+:13370
ENV OLLAMA_ENDPOINT=http://host.docker.internal:11434
ENV OLLAMA_MODEL=qwen3-vl:30b-a3b-instruct-q8_0
ENV ConnectionStrings__PersonalityDb=Host=postgres;Database=personality;Username=postgres;Password=postgres

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:13370/health || exit 1

ENTRYPOINT ["dotnet", "McpServer.dll"]
