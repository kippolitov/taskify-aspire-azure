# Quickstart: Deploy Taskify to Azure

**Phase**: 1 — Design & Contracts  
**Date**: March 6, 2026  
**Plan**: [plan.md](plan.md)

---

## Overview

This guide walks you through deploying the Taskify application to Azure using Azure Developer CLI (azd). You'll provision cloud infrastructure, migrate the database, and deploy the application containers.

**Time to complete**: 15-20 minutes  
**Cost estimate**: ~$25/month (development), ~$370/month (production)

---

## Prerequisites

Before you begin, ensure you have:

1. **Azure subscription** with Contributor access
2. **Azure CLI** (2.60+) — [Install](https://learn.microsoft.com/cli/azure/install-azure-cli)
3. **Azure Developer CLI (azd)** (1.9+) — [Install](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
4. **.NET SDK 10.0** — [Install](https://dotnet.microsoft.com/download/dotnet/10.0)
5. **Docker Desktop** (optional, for local testing) — [Install](https://www.docker.com/products/docker-desktop)
6. **Git** — [Install](https://git-scm.com/downloads)

**Verify installations**:
```bash
az --version              # Should show 2.60+
azd version               # Should show 1.9+
dotnet --version          # Should show 10.0.x
```

---

## Step 1: Clone and Setup Repository

```bash
# Clone the repository
git clone https://github.com/<your-org>/taskify.git
cd taskify

# Checkout the Azure deployment branch
git checkout 002-azure-hosting-cicd
```

---

## Step 2: Login to Azure

```bash
# Login to Azure CLI
az login

# Set your subscription (if you have multiple)
az account set --subscription "<your-subscription-id>"

# Verify active subscription
az account show
```

**Note your subscription ID** — you'll need it for azd configuration.

---

## Step 3: Initialize azd

```bash
# Initialize azd for this project
azd init

# When prompted:
# - Environment name: dev
# - Azure subscription: <select your subscription>
# - Azure location: eastus (or your preferred region)
```

This creates:
- `.azure/dev/.env` with your environment configuration
- Local azd state directory

---

## Step 4: Set Configuration Values

Configure environment-specific parameters:

```bash
# Set required environment variables
azd env set AZURE_ENV_NAME dev
azd env set AZURE_LOCATION eastus

# Generate a secure database password
POSTGRES_PASSWORD=$(openssl rand -base64 32)
azd env set POSTGRESQL_ADMIN_PASSWORD "$POSTGRES_PASSWORD"

# Optional: Configure resource sizing (defaults are for dev)
azd env set CONTAINER_APP_CPU 0.25
azd env set CONTAINER_APP_MEMORY 0.5Gi
```

---

## Step 5: Provision Azure Infrastructure

Deploy all Azure resources (Container Apps, PostgreSQL, Key Vault, etc.):

```bash
# Provision infrastructure (takes ~5-10 minutes)
azd provision
```

**What this does**:
1. Creates Resource Group (`rg-taskify-dev`)
2. Deploys Container Apps Environment
3. Provisions Azure PostgreSQL Flexible Server
4. Creates Azure Key Vault
5. Configures Application Insights
6. Sets up networking and access policies

**Output**: You'll see resource creation progress. Note the output URLs.

---

## Step 6: Run Database Migrations

Apply Entity Framework Core migrations to create database schema:

```bash
# Get the PostgreSQL connection string from azd outputs
CONNECTION_STRING=$(azd env get-values --output json | jq -r '.POSTGRESQL_CONNECTION_STRING')

# Run migrations
dotnet run --project src/Taskify.Migrator
```

**Verify migration success**:
```bash
# Connect to PostgreSQL and check tables
psql "$CONNECTION_STRING" -c "\dt"
# Should show: users, projects, task_items, comments, etc.
```

---

## Step 7: Deploy Applications

Build and deploy the API and Web applications to Azure Container Apps:

```bash
# Deploy applications (takes ~5-7 minutes)
azd deploy
```

**What this does**:
1. Builds .NET projects
2. Creates Docker containers
3. Pushes containers to Azure Container Registry (ACR)
4. Deploys containers to Container Apps
5. Configures environment variables and secrets
6. Updates ingress routes

**Output**: Deployment URLs for API and Web applications.

---

## Step 8: Verify Deployment

Check that your application is running:

```bash
# Get deployment URLs
API_URL=$(azd env get-values --output json | jq -r '.TASKIFY_API_URL')
WEB_URL=$(azd env get-values --output json | jq -r '.TASKIFY_WEB_URL')

# Test API health endpoint
curl "${API_URL}/health"
# Should return: {"status": "Healthy"}

# Test API data endpoint
curl "${API_URL}/api/tasks"
# Should return: JSON array of tasks

# Open web application in browser
open "$WEB_URL"  # macOS
# or
start "$WEB_URL"  # Windows
```

---

## Step 9: View Application Logs

Monitor application logs in real-time:

```bash
# View API logs
azd logs --service api --follow

# View Web logs
azd logs --service web --follow

# Or view in Azure Portal
az containerapp logs show \
  --name ca-taskify-api-dev \
  --resource-group rg-taskify-dev
```

---

## Step 10: Access Azure Resources

### Azure Portal
```bash
# Open resource group in Azure Portal
az group show --name rg-taskify-dev --query id -o tsv | xargs -I {} open "https://portal.azure.com/#@/resource{}"
```

### Application Insights
```bash
# Open Application Insights
az monitor app-insights component show \
  --app appi-taskify-dev \
  --resource-group rg-taskify-dev \
  --query id -o tsv | xargs -I {} open "https://portal.azure.com/#@/resource{}"
```

### Database Management
```bash
# Connect to PostgreSQL via psql
psql "$(azd env get-values --output json | jq -r '.POSTGRESQL_CONNECTION_STRING')"
```

---

## Complete Deployment in One Command

For subsequent deployments, use:

```bash
# Provision infrastructure + deploy applications
azd up

# This is equivalent to:
# azd provision && azd deploy
```

---

## Deploy to Production

To deploy to a production environment:

```bash
# Create production environment
azd env new prod

# Set production configuration
azd env set AZURE_ENV_NAME prod
azd env set AZURE_LOCATION eastus
azd env set CONTAINER_APP_CPU 1.0
azd env set CONTAINER_APP_MEMORY 2Gi
azd env set CONTAINER_APP_MIN_REPLICAS 1
azd env set POSTGRESQL_SKU_NAME Standard_D2s_v3
azd env set POSTGRESQL_TIER GeneralPurpose
azd env set POSTGRESQL_HIGH_AVAILABILITY ZoneRedundant

# Generate production database password
POSTGRES_PASSWORD=$(openssl rand -base64 32)
azd env set POSTGRESQL_ADMIN_PASSWORD "$POSTGRES_PASSWORD"

# Deploy to production
azd up --environment prod
```

---

## Automated Deployment via GitHub Actions

### Setup GitHub Actions

1. **Create Azure Service Principal for GitHub Actions**:

```bash
# Create Service Principal with Contributor role
az ad sp create-for-rbac \
  --name "GitHub-Actions-Taskify" \
  --role Contributor \
  --scopes /subscriptions/<subscription-id> \
  --sdk-auth

# Save the JSON output
```

2. **Configure GitHub Secrets**:

```bash
# Set repository secrets
gh secret set AZURE_CREDENTIALS --body '<service-principal-json>'
gh secret set AZURE_SUBSCRIPTION_ID --body '<subscription-id>'

# Set environment-specific secrets
gh secret set POSTGRESQL_ADMIN_PASSWORD --env development --body '<dev-password>'
gh secret set POSTGRESQL_ADMIN_PASSWORD --env production --body '<prod-password>'
```

3. **Trigger deployment**:

```bash
# Push to main branch triggers dev deployment
git push origin main

# Manual production deployment via GitHub UI
# Go to Actions → Azure Deployment → Run workflow → Select 'prod'
```

### Alternative: OIDC Authentication (Recommended)

For password-less authentication:

```bash
# Create Azure AD App Registration
APP_ID=$(az ad app create --display-name "GitHub-Taskify" --query appId -o tsv)

# Create Service Principal
az ad sp create --id "$APP_ID"

# Create Federated Credential
az ad app federated-credential create \
  --id "$APP_ID" \
  --parameters '{
    "name": "github-deploy",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:<org>/<repo>:environment:production",
    "audiences": ["api://AzureADTokenExchange"]
  }'

# Assign Contributor role
az role assignment create \
  --assignee "$APP_ID" \
  --role Contributor \
  --scope /subscriptions/<subscription-id>

# Set GitHub secrets (no password needed!)
gh secret set AZURE_CLIENT_ID --body "$APP_ID"
gh secret set AZURE_TENANT_ID --body "$(az account show --query tenantId -o tsv)"
gh secret set AZURE_SUBSCRIPTION_ID --body "<subscription-id>"
```

---

## Troubleshooting

### Issue: `azd provision` fails with authentication error

**Solution**:
```bash
# Re-authenticate
az login
azd auth login

# Verify subscription
az account show
```

### Issue: Database migrations fail

**Solution**:
```bash
# Check connection string
azd env get-values | grep POSTGRESQL_CONNECTION_STRING

# Test connection
psql "$(azd env get-values --output json | jq -r '.POSTGRESQL_CONNECTION_STRING')" -c "SELECT version();"

# Check PostgreSQL firewall rules
az postgres flexible-server firewall-rule list \
  --resource-group rg-taskify-dev \
  --name psql-taskify-dev-<hash>
```

### Issue: Container Apps show "Provisioning Failed"

**Solution**:
```bash
# Check Container App logs
az containerapp logs show \
  --name ca-taskify-api-dev \
  --resource-group rg-taskify-dev \
  --tail 100

# Check Container App revision status
az containerapp revision list \
  --name ca-taskify-api-dev \
  --resource-group rg-taskify-dev \
  --output table
```

### Issue: Application returns 500 errors

**Solution**:
```bash
# Check Application Insights for exceptions
# Go to Azure Portal → Application Insights → Failures

# View live metrics
az monitor app-insights metrics show \
  --app appi-taskify-dev \
  --resource-group rg-taskify-dev \
  --metrics exceptions/count
```

---

## Clean Up Resources

To delete all Azure resources and avoid charges:

```bash
# Delete all resources (WARNING: irreversible)
azd down

# Or delete resource group manually
az group delete --name rg-taskify-dev --yes --no-wait
```

---

## Next Steps

- **Configure custom domain**: Set up a custom domain for your Container Apps
- **Enable auto-scaling**: Configure scaling rules based on HTTP traffic
- **Set up monitoring alerts**: Create alerts for errors, performance degradation
- **Implement staged deployments**: Use blue-green deployment for zero-downtime updates
- **Optimize costs**: Review Azure Cost Management and enable scale-to-zero for dev

---

## Common Commands Reference

```bash
# View environment configuration
azd env get-values

# Set environment variable
azd env set KEY value

# Switch between environments
azd env select dev
azd env select prod

# View deployment logs
azd logs --service api --follow

# Re-deploy application only (skip infrastructure)
azd deploy

# Update infrastructure only (skip application)
azd provision

# Complete teardown
azd down --force --purge
```

---

## Cost Monitoring

Monitor your Azure spending:

```bash
# View current month costs
az consumption usage list \
  --start-date $(date -u -d "1 day ago" '+%Y-%m-%d') \
  --end-date $(date -u '+%Y-%m-%d') \
  --query "[?contains(instanceName, 'taskify')].{Name:instanceName, Cost:pretaxCost}" \
  --output table

# Set up budget alert
az consumption budget create \
  --budget-name taskify-dev-budget \
  --amount 50 \
  --time-period $(date '+%Y-%m-01') to $(date -d '+1 month' '+%Y-%m-01') \
  --resource-group rg-taskify-dev
```

---

## Support

- **Azure documentation**: [https://learn.microsoft.com/azure](https://learn.microsoft.com/azure)
- **azd documentation**: [https://learn.microsoft.com/azure/developer/azure-developer-cli](https://learn.microsoft.com/azure/developer/azure-developer-cli)
- **.NET Aspire documentation**: [https://learn.microsoft.com/dotnet/aspire](https://learn.microsoft.com/dotnet/aspire)
- **Team wiki**: `docs/deployment.md` (once created)

---

## Summary

You've successfully:
- ✅ Provisioned Azure infrastructure with Container Apps, PostgreSQL, and Key Vault
- ✅ Deployed the Taskify API and Web applications
- ✅ Configured monitoring with Application Insights
- ✅ Set up automated CI/CD with GitHub Actions (optional)

Your Taskify application is now running in Azure! 🎉
