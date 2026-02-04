# OpenClaw Gateway Docker Setup
FROM node:22-slim

# Install dependencies
RUN apt-get update && apt-get install -y \
    chromium \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Set Puppeteer to use installed Chromium
ENV PUPPETEER_SKIP_CHROMIUM_DOWNLOAD=true
ENV PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium

WORKDIR /app

# Install OpenClaw and mcporter globally
RUN npm install -g openclaw mcporter

# Create directories for persistent data
RUN mkdir -p /root/.openclaw /root/.mcporter /root/.openclaw/workspace

# Copy mcporter config (points to MCP server)
COPY openclaw/mcporter.json /root/.mcporter/mcporter.json

# Copy OpenClaw workspace (SOUL.md, etc.)
COPY openclaw/workspace/ /root/.openclaw/workspace/

# Expose gateway port
EXPOSE 18789

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:18789/health || exit 1

# Start OpenClaw gateway
CMD ["openclaw", "gateway"]
