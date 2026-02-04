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
COPY mcporter.json /root/.mcporter/mcporter.json

# Copy OpenClaw workspace (SOUL.md, etc.)
COPY workspace/ /root/.openclaw/workspace/

# Expose gateway port
EXPOSE 18789

# Version: 1.0.1 - Railway deploy trigger
CMD ["openclaw", "gateway"]
