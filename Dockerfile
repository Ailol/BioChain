# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Copy everything
COPY . .

# Restore and publish
RUN dotnet publish McpServer/McpServer.csproj -c Release -o /app/publish

# Runtime stage - Alpine (small ~100MB)
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

RUN apk add --no-cache curl

COPY --from=build /app/publish .

EXPOSE 13370

ENV ASPNETCORE_URLS=http://+:13370

ENTRYPOINT ["dotnet", "McpServer.dll"]
