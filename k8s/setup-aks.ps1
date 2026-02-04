# AKS Setup Script for PowerShell
# Run after installing Azure CLI: winget install Microsoft.AzureCLI

# Variables - EDIT THESE
$RESOURCE_GROUP = "openclaw-rg"
$CLUSTER_NAME = "openclaw-aks"
$LOCATION = "westeurope"

# Login to Azure
az login

# 1. Create Resource Group
az group create --name $RESOURCE_GROUP --location $LOCATION

# 2. Create AKS Cluster (FREE tier + cheapest node)
az aks create `
  --resource-group $RESOURCE_GROUP `
  --name $CLUSTER_NAME `
  --node-count 1 `
  --node-vm-size Standard_B1ms `
  --tier free `
  --generate-ssh-keys `
  --enable-managed-identity

# 3. Get credentials (connects kubectl to your cluster)
az aks get-credentials --resource-group $RESOURCE_GROUP --name $CLUSTER_NAME

# 4. Verify connection
kubectl get nodes

Write-Host ""
Write-Host "AKS cluster created! Now run:" -ForegroundColor Green
Write-Host '  kubectl create secret generic openclaw-secrets --from-literal=anthropic-api-key=YOUR_API_KEY'
Write-Host '  kubectl apply -f k8s/openclaw-deployment.yaml'
