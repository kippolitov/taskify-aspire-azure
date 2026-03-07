#!/bin/bash
set -e

echo "========================================="
echo "Pre-deployment Validation"
echo "========================================="

# Check Azure CLI is installed and authenticated
echo "Checking Azure CLI..."
if ! command -v az &> /dev/null; then
    echo "ERROR: Azure CLI not found. Please install Azure CLI: https://aka.ms/install-azure-cli"
    exit 1
fi

echo "Verifying Azure CLI authentication..."
if ! az account show &> /dev/null; then
    echo "ERROR: Not logged in to Azure. Please run 'az login' or 'azd auth login'"
    exit 1
fi

# Display current Azure subscription
SUBSCRIPTION_NAME=$(az account show --query name -o tsv)
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
echo "✓ Authenticated to Azure subscription: $SUBSCRIPTION_NAME ($SUBSCRIPTION_ID)"

# Validate Bicep templates
echo ""
echo "Validating Bicep templates..."
if ! command -v az bicep &> /dev/null; then
    echo "ERROR: Bicep CLI not found. Please install: https://aka.ms/install-bicep"
    exit 1
fi

echo "Building main.bicep..."
az bicep build --file ./infra/main.bicep

echo "Building module: monitoring.bicep..."
az bicep build --file ./infra/resources/monitoring.bicep

echo "Building module: acr.bicep..."
az bicep build --file ./infra/resources/acr.bicep

# Temporarily disabled - Key Vault module not in use
# echo "Building module: keyvault.bicep..."
# az bicep build --file ./infra/resources/keyvault.bicep

echo "Building module: postgresql.bicep..."
az bicep build --file ./infra/resources/postgresql.bicep

echo "Building module: container-apps.bicep..."
az bicep build --file ./infra/resources/container-apps.bicep

echo "✓ All Bicep templates validated successfully"

# Check Docker is available (for building container images)
echo ""
echo "Checking Docker..."
if ! command -v docker &> /dev/null; then
    echo "WARNING: Docker not found. Container image builds may fail."
    echo "Install Docker: https://docs.docker.com/get-docker/"
else
    if ! docker info &> /dev/null; then
        echo "WARNING: Docker daemon is not running. Please start Docker."
    else
        echo "✓ Docker is available and running"
    fi
fi

echo ""
echo "========================================="
echo "Pre-deployment validation PASSED"
echo "========================================="
