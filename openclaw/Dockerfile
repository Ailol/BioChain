# OpenClaw Gateway - Debian slim
FROM node:22-slim

# Install dependencies (including openssh for git)
RUN apt-get update && apt-get install -y --no-install-recommends \
    chromium \
    git \
    openssh-client \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Set Puppeteer to use installed Chromium
ENV PUPPETEER_SKIP_CHROMIUM_DOWNLOAD=true
ENV PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium
ENV NODE_OPTIONS="--max-old-space-size=1400"
ENV NODE_LLAMA_CPP_SKIP_DOWNLOAD=true

WORKDIR /app

# Configure git to use https instead of ssh
RUN git config --global url."https://github.com/".insteadOf "ssh://git@github.com/"

# Install OpenClaw and mcporter
RUN npm install -g openclaw mcporter --ignore-scripts && \
    npm cache clean --force

# Create directories
RUN mkdir -p /root/.openclaw /root/.mcporter /root/.openclaw/workspace

# Copy configs
COPY mcporter.json /root/.mcporter/mcporter.json
COPY openclaw.json /root/.openclaw/openclaw.json
COPY workspace/ /root/.openclaw/workspace/

EXPOSE 18789

CMD ["openclaw", "gateway"]
