# Kubernetes Deployments

## Services

| Service | Type | Notes |
|---------|------|-------|
| mcpserver | Helm chart | Auto-deployed on push to main |
| openclaw | External Helm | Manual: `helm install openclaw openclaw/openclaw` |
| open-webui | YAML | Manual: `kubectl apply -f open-webui-deployment.yaml` |

## Setup

### 1. Install Ingress Controller
```bash
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.9.4/deploy/static/provider/cloud/deploy.yaml
```

### 2. Deploy MCP Server (Helm)
```bash
helm upgrade --install mcpserver ./charts/mcpserver \
  --set secrets.postgresConnectionString="YOUR_CONNECTION_STRING"
```

### 3. Deploy OpenClaw (External Helm)
```bash
helm repo add openclaw https://chrisbattarbee.github.io/openclaw-helm
helm repo update
helm install openclaw openclaw/openclaw --set credentials.anthropicApiKey=YOUR_API_KEY

# Run onboarding
kubectl exec -it deploy/openclaw -c openclaw -- node dist/index.js onboard
```

### 4. Deploy Open WebUI
```bash
kubectl apply -f open-webui-deployment.yaml
```

### 5. Apply Ingress
```bash
kubectl apply -f ingress.yaml
```

## DNS Records (Domeneshop)

| Subdomain | Type | Value |
|-----------|------|-------|
| chat | A | 51.105.166.178 |
| mcp | A | 51.105.166.178 |
| openclaw | A | 51.105.166.178 |

## GitHub Secrets Required

- `DOCKERHUB_TOKEN` - Docker Hub access token
- `KUBECONFIG` - Base64 encoded kubeconfig
- `POSTGRES_CONNECTION_STRING` - Supabase PostgreSQL connection string
