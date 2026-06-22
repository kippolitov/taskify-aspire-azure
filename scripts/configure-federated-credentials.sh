#!/bin/bash

# Configure GitHub Actions OIDC Federated Credentials for Azure
# This script adds federated credentials to an Azure App Registration for GitHub Actions

set -e

# Configuration - Replace these values
APP_NAME="GitHub-Taskify"  # Your App Registration display name
GITHUB_ORG="kippolitov"
GITHUB_REPO="taskify-aspire-azure"

echo "🔍 Finding App Registration: $APP_NAME"
APP_OBJECT_ID=$(az ad app list --display-name "$APP_NAME" --query "[0].id" -o tsv)

if [ -z "$APP_OBJECT_ID" ]; then
  echo "❌ App Registration '$APP_NAME' not found!"
  echo "Available app registrations:"
  az ad app list --query "[].{Name:displayName, AppId:appId}" -o table
  exit 1
fi

APP_ID=$(az ad app list --display-name "$APP_NAME" --query "[0].appId" -o tsv)
echo "✅ Found App Registration"
echo "   Object ID: $APP_OBJECT_ID"
echo "   App ID: $APP_ID"

# Add federated credential for main branch
echo ""
echo "📝 Adding federated credential for main branch..."
az ad app federated-credential create \
  --id "$APP_OBJECT_ID" \
  --parameters '{
    "name": "github-actions-main-branch",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:'"$GITHUB_ORG"'/'"$GITHUB_REPO"':ref:refs/heads/main",
    "audiences": [
      "api://AzureADTokenExchange"
    ],
    "description": "GitHub Actions OIDC for main branch deployments"
  }' || echo "⚠️  Credential may already exist for main branch"

# Add federated credential for production environment
echo ""
echo "📝 Adding federated credential for production environment..."
az ad app federated-credential create \
  --id "$APP_OBJECT_ID" \
  --parameters '{
    "name": "github-actions-production-env",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:'"$GITHUB_ORG"'/'"$GITHUB_REPO"':environment:production",
    "audiences": [
      "api://AzureADTokenExchange"
    ],
    "description": "GitHub Actions OIDC for production environment deployments"
  }' || echo "⚠️  Credential may already exist for production environment"

echo ""
echo "✅ Federated credentials configured!"
echo ""
echo "📋 Verify credentials:"
az ad app federated-credential list --id "$APP_OBJECT_ID" -o table

echo ""
echo "🔐 Make sure these GitHub secrets are configured:"
echo "   AZURE_CLIENT_ID: $APP_ID"
echo "   AZURE_TENANT_ID: $(az account show --query tenantId -o tsv)"
echo "   AZURE_SUBSCRIPTION_ID: $(az account show --query id -o tsv)"
