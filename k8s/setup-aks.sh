#!/bin/bash
# AKS Setup Script - Run in Azure Cloud Shell or local Azure CLI

# Variables - EDIT THESE
RESOURCE_GROUP="openclaw-rg"
CLUSTER_NAME="openclaw-aks"
LOCATION="westeurope"
ACR_NAME="openclawacr$(shuf -i 1000-9999 -n 1)"  # Random suffix for uniqueness

# 1. Create Resource Group
az group create --name $RESOURCE_GROUP --location $LOCATION

# 2. Create AKS Cluster (FREE tier + cheapest node)
az aks create \
  --resource-group $RESOURCE_GROUP \
  --name $CLUSTER_NAME \
  --node-count 1 \
  --node-vm-size Standard_B1ms \
  --tier free \
  --generate-ssh-keys \
  --enable-managed-identity

# 3. Get credentials
az aks get-credentials --resource-group $RESOURCE_GROUP --name $CLUSTER_NAME

# 4. Verify connection
kubectl get nodes

echo "AKS cluster created! Now run:"
echo "  kubectl create secret generic openclaw-secrets --from-literal=anthropic-api-key=YOUR_KEY"
echo "  kubectl apply -f k8s/openclaw-deployment.yaml"
