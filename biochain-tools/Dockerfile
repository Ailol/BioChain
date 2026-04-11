FROM rust:1.82-bookworm AS builder
WORKDIR /app
COPY Cargo.toml Cargo.lock* ./
COPY src/ src/
COPY registry/ registry/
RUN cargo build --release

FROM debian:bookworm-slim
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates curl && rm -rf /var/lib/apt/lists/*
COPY --from=builder /app/target/release/biochain-tools /usr/local/bin/biochain-tools
ENV BIOCHAIN_TOOLS_PORT=8002
ENV BIOCHAIN_TOOLS_DB=/data/cache.db
EXPOSE 8002
CMD ["biochain-tools"]
