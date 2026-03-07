# Azure Deployment Guide for Taskify

**Last Updated**: March 6, 2026  
**Audience**: DevOps Engineers, Platform Engineers, Developers  
**Prerequisites**: Azure subscription, Azure CLI, Azure Developer CLI (azd), .NET SDK 10.0

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Prerequisites](#prerequisites)
4. [Initial Setup](#initial-setup)
5. [Local Development](#local-development)
6. [Azure Deployment](#azure-deployment)
   - [Development Environment](#development-environment)
   - [Production Environment](#production-environment)
7. [CI/CD Pipeline](#cicd-pipeline)
8. [Infrastructure Management](#infrastructure-management)
9. [Monitoring & Observability](#monitoring--observability)
10. [Security](#security)
11. [Cost Optimization](#cost-optimization)
12. [Troubleshooting](#troubleshooting)
13. [Operational Runbooks](#operational-runbooks)

---

## Overview

Taskify is a collaborative task management application built with .NET 10, Blazor, and PostgreSQL. It deploys to Azure using:

- **Azure Container Apps**: Serverless compute for API and Web applications
- **Azure PostgreSQL Flexible Server**: Managed database with automated backups
- **Azure Key Vault**: Secrets management
- **Application Insights**: Application performance monitoring
- **Azure Container Registry**: Container image storage (managed by Container Apps)

**Deployment Strategy**: Infrastructure as Code (Bicep) + Azure Developer CLI (azd) + GitHub Actions

---

## Architecture

### Logical Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      Azure Subscription                         │
│                                                                 │
│  ┌───────────────────────────────────────────────────────────┐ │
│  │ Resource Group: rg-taskify-{env}-{uniqueId}              │ │
│  │                                                            │ │
│  │  ┌──────────────────┐      ┌──────────────────┐          │ │
│  │  │ Container Apps   │◄─────┤ Container Apps   │          │ │
│  │  │ Environment      │      │ (API & Web)      │          │ │
│  │  └──────────────────┘      └──────────────────┘          │ │
│  │           │                         │                     │ │
│  │           └────────┬────────────────┘                     │ │
│  │                    │                                      │ │
│  │         ┌──────────▼──────────┐                          │ │
│  │         │ PostgreSQL Flexible │                          │ │
│  │         │ Server              │                          │ │
│  │         └─────────────────────┘                          │ │
│  │                    │                                      │ │
│  │         ┌──────────▼──────────┐    ┌─────────────────┐  │ │
│  │         │ Key Vault           │────┤ Application     │  │ │
│  │         │ (Secrets)           │    │ Insights        │  │ │
│  │         └─────────────────────┘    └─────────────────┘  │ │
│  │                                            │             │ │
│  │                                    ┌───────▼───────┐    │ │
│  │                                    │ Log Analytics │    │ │
│  │                                    │ Workspace     │    │ │
│  │                                    └───────────────┘    │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

### Network Architecture

**Default (No VNet)**:
- Container Apps: Public ingress (HTTPS only)
- PostgreSQL: Public endpoint with Azure firewall rules
- Key Vault: Public endpoint with access policies

**With VNet Integration** (optional for production):
- Virtual Network with delegated subnets
- Private endpoints for PostgreSQL and Key Vault
- Network Security Groups for traffic filtering
- Container Apps Environment integrated with VNet

---

## Prerequisites

### Required Tools

| Tool | Minimum Version | Installation |
|------|----------------|--------------|
| Azure CLI | 2.60+ | https://learn.microsoft.com/cli/azure/install-azure-cli |
| Azure Developer CLI (azd) | 1.9+ | https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd |
| .NET SDK | 10.0 | https://dotnet.microsoft.com/download/dotnet/10.0 |
| Docker Desktop | 4.x | https://www.docker.com/products/docker-desktop/ |
| Git | 2.x | https://git-scm.com/downloads |
| PowerShell | 7.x (cross-platform) | https://learn.microsoft.com/powershell/scripting/install/installing-powershell |

### Azure Requirements

- **Subscription**: Contributor or Owner role
- **Resource Providers**: Registered for:
  - `Microsoft.App` (Container Apps)
  - `Microsoft.DBforPostgreSQL` (PostgreSQL)
  - `Microsoft.KeyVault` (Key Vault)
  - `Microsoft.OperationalInsights` (Log Analytics)
  - `Microsoft.Insights` (Application Insights)

**Register providers**:
```bash
az provider register --namespace Microsoft.App
az provider register --namespace Microsoft.DBforPostgreSQL
az provider register --namespace Microsoft.KeyVault
az provider register --namespace Microsoft.OperationalInsights
az provider register --namespace Microsoft.Insights
```

### Permissions Required

- Create resource groups
- Deploy Azure resources (Container Apps, PostgreSQL, Key Vault)
- Assign managed identity roles
- Configure networking (if VNet enabled)

---

## Initial Setup

### 1. Clone Repository

```bash
git clone https://github.com/<your-org>/taskify.git
cd taskify
```

### 2. Verify Prerequisites

```bash
# Check tool versions
az --version              # Should show 2.60+
azd version               # Should show 1.9+
dotnet --version          # Should show 10.0.x
docker --version          # Should show 4.x+

# Login to Azure
az login
azd auth login

# Set active subscription
az account set --subscription "<subscription-id>"
az account show
```

### 3. Initialize azd Environment

```bash
# Initialize for development
azd init

# When prompted:
# - Environment name: dev
# - Subscription: <select your subscription>
# - Location: eastus (or your preferred region)

# Configure environment variables
azd env set AZURE_ENV_NAME dev
azd env set AZURE_LOCATION eastus

# Generate secure database password
POSTGRES_PASSWORD=$(openssl rand -base64 32)
azd env set POSTGRESQL_ADMIN_PASSWORD "$POSTGRES_PASSWORD"
```

---

## Local Development

### Run with .NET Aspire

```bash
# Start all services (API, Web, PostgreSQL container)
dotnet run --project src/Taskify.AppHost

# Aspire dashboard: https://localhost:15000
# API: Check dashboard for port
# Web: Check dashboard for port
```

### Run Tests

```bash
# All tests
dotnet test

# Specific test projects
dotnet test tests/Taskify.Api.Tests
dotnet test tests/Taskify.Web.Tests

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Docker Build Locally

```bash
# Build API image
docker build -f src/Taskify.Api/Dockerfile -t taskify-api:local .

# Build Web image
docker build -f src/Taskify.Web/Dockerfile -t taskify-web:local .

# Run locally
docker run -p 8080:8080 taskify-api:local
```

---

## Azure Deployment

### Development Environment

**Estimated Cost**: ~$18-25/month

**Configuration**:
- PostgreSQL: Burstable B1ms (1 vCore, 2 GB RAM)
- Container Apps: 0.25 vCPU, 0.5 GiB memory
- Scale-to-zero: Enabled (no charges when idle)
- High Availability: Disabled
- Backup Retention: 7 days

**Deploy**:

```bash
# Full deployment (infrastructure + applications)
azd up

# Or step-by-step:
azd provision  # Infrastructure only (~5-10 minutes)
azd deploy     # Applications only (~5-7 minutes)
```

**Verify Deployment**:

```bash
# Get deployment URLs
API_URL=$(azd env get-values --output json | jq -r '.TASKIFY_API_URL')
WEB_URL=$(azd env get-values --output json | jq -r '.TASKIFY_WEB_URL')

# Test endpoints
curl "${API_URL}/health"
open "$WEB_URL"  # macOS
```

### Production Environment

**Estimated Cost**: ~$370/month

**Configuration**:
- PostgreSQL: General Purpose D2s_v3 (2 vCores, 8 GB RAM)
- Container Apps: 1.0 vCPU, 2 GiB memory
- Scale-to-zero: Disabled (min 1 replica)
- High Availability: Zone-redundant
- Backup Retention: 35 days
- Storage: 128 GB (vs 32 GB dev)

**Deploy**:

```bash
# Create production environment
azd env new prod

# Configure production parameters
azd env set AZURE_ENV_NAME prod
azd env set AZURE_LOCATION eastus
azd env set CONTAINER_APP_CPU 1.0
azd env set CONTAINER_APP_MEMORY 2Gi
azd env set CONTAINER_APP_MIN_REPLICAS 1
azd env set POSTGRESQL_SKU_NAME Standard_D2s_v3
azd env set POSTGRESQL_TIER GeneralPurpose
azd env set POSTGRESQL_HIGH_AVAILABILITY ZoneRedundant

# Set production database password
POSTGRES_PASSWORD=$(openssl rand -base64 32)
azd env set POSTGRESQL_ADMIN_PASSWORD "$POSTGRES_PASSWORD"
# IMPORTANT: Store this password securely (e.g., Azure Key Vault, 1Password)

# Deploy to production
azd up --environment prod
```

**Production Deployment Checklist**:
- ✅ Database password stored securely
- ✅ Backup notifications configured
- ✅ Cost alerts enabled
- ✅ Application Insights monitoring active
- ✅ DNS/custom domain configured (if needed)
- ✅ GitHub Environment protection rules set
- ✅ Rollback plan documented

---

## CI/CD Pipeline

### GitHub Actions Setup

**Workflows**:
1. **CI** (`.github/workflows/ci.yml`): Build, test, lint, validate Bicep
2. **Azure Deployment** (`.github/workflows/azure-dev.yml`): Deploy to Azure
3. **Benchmarks** (`.github/workflows/benchmark.yml`): Performance validation

### OIDC Authentication (Password-less)

**One-time setup**:

```bash
# Create Azure AD App Registration
APP_ID=$(az ad app create --display-name "GitHub-Taskify" --query appId -o tsv)

# Create Service Principal
SP_ID=$(az ad sp create --id "$APP_ID" --query id -o tsv)

# Get subscription and tenant IDs
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
TENANT_ID=$(az account show --query tenantId -o tsv)

# Assign Contributor role
az role assignment create \
  --assignee "$APP_ID" \
  --role Contributor \
  --scope /subscriptions/$SUBSCRIPTION_ID

# Create Federated Credential for main branch
az ad app federated-credential create \
  --id "$APP_ID" \
  --parameters '{
    "name": "github-main",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:<org>/<repo>:ref:refs/heads/main",
    "audiences": ["api://AzureADTokenExchange"]
  }'

# Create Federated Credential for production environment
az ad app federated-credential create \
  --id "$APP_ID" \
  --parameters '{
    "name": "github-prod",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:<org>/<repo>:environment:production",
    "audiences": ["api://AzureADTokenExchange"]
  }'
```

**Configure GitHub Secrets**:

```bash
# Set repository secrets
gh secret set AZURE_CLIENT_ID --body "$APP_ID"
gh secret set AZURE_TENANT_ID --body "$TENANT_ID"
gh secret set AZURE_SUBSCRIPTION_ID --body "$SUBSCRIPTION_ID"

# Set environment-specific secrets
gh secret set POSTGRESQL_ADMIN_PASSWORD --env development --body "$DEV_PASSWORD"
gh secret set POSTGRESQL_ADMIN_PASSWORD --env production --body "$PROD_PASSWORD"
```

### Deployment Triggers

- **Automatic**: Push to `main` → Deploy to development
- **Manual**: Workflow dispatch → Deploy to production (requires approval)
- **CI**: Pull requests → Build, test, validate only

---

## Infrastructure Management

### Update Infrastructure

```bash
# 1. Modify Bicep templates in infra/
# 2. Validate changes
az bicep build --file infra/main.bicep

# 3. Preview changes (What-If)
az deployment group what-if \
  --resource-group rg-taskify-dev \
  --template-file infra/main.bicep \
  --parameters infra/main.parameters.dev.json

# 4. Apply changes
azd provision
```

### Scale Resources

**Horizontal scaling** (Container Apps replicas):
```bash
# Update parameter file
"containerAppMinReplicas": { "value": 2 },
"containerAppMaxReplicas": { "value": 30 }

# Redeploy
azd provision
```

**Vertical scaling** (CPU/memory):
```bash
# Update parameter file
"containerAppCpu": { "value": 1.0 },
"containerAppMemory": { "value": "2Gi" }

# Redeploy
azd provision
```

**Database scaling**:
```bash
# Update parameter file
"postgresqlSkuName": { "value": "Standard_D4s_v3" },
"postgresqlStorageSizeGB": { "value": 256 }

# Redeploy (minimal downtime)
azd provision
```

### Enable High Availability

```bash
# Update parameter file
"postgresqlHighAvailabilityMode": { "value": "ZoneRedundant" }

# Deploy (creates standby replica)
azd provision
```

### Backup and Restore

**Create manual backup**:
```bash
az postgres flexible-server backup create \
  --resource-group rg-taskify-prod \
  --name psql-taskify-prod-<uniqueId> \
  --backup-name manual-backup-$(date +%Y%m%d-%H%M%S)
```

**List backups**:
```bash
az postgres flexible-server backup list \
  --resource-group rg-taskify-prod \
  --name psql-taskify-prod-<uniqueId> \
  --output table
```

**Restore from backup**:
```bash
# Restore to new server
az postgres flexible-server restore \
  --resource-group rg-taskify-prod \
  --name psql-taskify-prod-restored \
  --source-server psql-taskify-prod-<uniqueId> \
  --restore-time "2026-03-06T12:00:00Z"
```

---

## Monitoring & Observability

### Application Insights

**Access**:
```bash
# Open in Azure Portal
az monitor app-insights component show \
  --app appi-taskify-dev \
  --resource-group rg-taskify-dev \
  --query id -o tsv | xargs -I {} open "https://portal.azure.com/#@/resource{}"
```

**Key Metrics**:
- Request rate, response time, failure rate
- Exception count and types
- Dependency metrics (PostgreSQL queries)
- Custom events (SignalR connections, task operations)

**Query Logs** (Kusto Query Language):
```kusto
// Exceptions in last 24 hours
exceptions
| where timestamp > ago(24h)
| summarize count() by type, outerMessage
| order by count_ desc

// Slow API requests
requests
| where timestamp > ago(1h)
| where duration > 2000  // >2 seconds
| project timestamp, name, duration, resultCode
| order by duration desc
```

### Log Streaming

```bash
# Real-time logs from Container Apps
azd logs --service api --follow
azd logs --service web --follow

# Or via Azure CLI
az containerapp logs tail \
  --name ca-taskify-api-dev \
  --resource-group rg-taskify-dev \
  --follow
```

### Alerts

**Exception alerts**: Triggered when exception count exceeds 10 in 15 minutes  
**Response time alerts**: Triggered when average response time >2 seconds  
**Availability alerts**: Triggered when availability <99%

**Configure email notifications**:
```bash
# Update parameter file
"budgetAlertEmails": { "value": "team@example.com,oncall@example.com" }

# Redeploy monitoring
azd provision
```

---

## Security

### Managed Identities

All Azure resources use **System-Assigned Managed Identities** (no passwords):
- Container Apps → Key Vault (secrets access)
- Container Apps → PostgreSQL (database access)

**Verify identities**:
```bash
az containerapp identity show \
  --name ca-taskify-api-dev \
  --resource-group rg-taskify-dev
```

### Key Vault Access

**Access policies**:
- Container Apps: Get secrets
- Deployment pipeline: List, set secrets

**Audit access**:
```bash
az monitor diagnostic-settings create \
  --resource <keyvault-id> \
  --name audit-logs \
  --workspace <log-analytics-workspace-id> \
  --logs '[{"category": "AuditEvent", "enabled": true}]'
```

### Network Security

**With VNet integration**:
- All traffic flows through private network
- PostgreSQL has no public endpoint
- NSG rules control inbound/outbound traffic

**Enable VNet**:
```bash
# Update parameter file
"enableVNetIntegration": { "value": true }

# Deploy networking module
azd provision
```

### Secrets Management

**Never commit secrets**:
- Use `.env` files (excluded by `.gitignore`)
- Store in Azure Key Vault
- Reference via `@Microsoft.KeyVault(SecretUri=...)`

**Rotate secrets**:
```bash
# Generate new database password
NEW_PASSWORD=$(openssl rand -base64 32)

# Update in Key Vault
az keyvault secret set \
  --vault-name kv-taskify-prod-<uniqueId> \
  --name postgresql-password \
  --value "$NEW_PASSWORD"

# Update PostgreSQL
az postgres flexible-server update \
  --resource-group rg-taskify-prod \
  --name psql-taskify-prod-<uniqueId> \
  --admin-password "$NEW_PASSWORD"

# Restart Container Apps to pick up new secret
az containerapp revision restart \
  --name ca-taskify-api-prod-<uniqueId> \
  --resource-group rg-taskify-prod
```

---

## Cost Optimization

### Development Environment

**Cost-saving strategies**:
- Scale-to-zero enabled (Container Apps only charge when running)
- Burstable PostgreSQL tier (cheaper than General Purpose)
- Stop development environment overnight

**Stop resources**:
```bash
# Stop PostgreSQL (still incurs storage charges)
az postgres flexible-server stop \
  --resource-group rg-taskify-dev \
  --name psql-taskify-dev-<uniqueId>

# Scale Container Apps to zero
az containerapp update \
  --name ca-taskify-api-dev \
  --resource-group rg-taskify-dev \
  --min-replicas 0 --max-replicas 0
```

**Budget alerts**:
```bash
# Set monthly budget ($50 for dev)
az consumption budget create \
  --budget-name taskify-dev-budget \
  --amount 50 \
  --time-period $(date '+%Y-%m-01') to $(date -d '+1 month' '+%Y-%m-01') \
  --resource-group rg-taskify-dev
```

### Production Environment

**Right-sizing**:
- Start with minimum required SKUs
- Monitor metrics (CPU, memory, IOPS)
- Scale up/down based on actual usage

**Reserved capacity**:
- Azure Reservations (1 or 3-year commitments) offer up to 65% savings
- Consider for stable production workloads

---

## Troubleshooting

### Common Issues

#### Issue: `azd provision` fails with "Resource name already exists"

**Solution**:
```bash
# Delete existing resource group
az group delete --name rg-taskify-dev --yes

# Or use a different uniqueId
azd provision --parameters uniqueId=$(openssl rand -hex 4)
```

#### Issue: Container Apps show 503 errors

**Possible causes**:
1. Application not fully started (wait 30-60 seconds)
2. Health endpoint failing
3. Environment variables misconfigured

**Solution**:
```bash
# Check revision status
az containerapp revision list \
  --name ca-taskify-api-dev \
  --resource-group rg-taskify-dev \
  --output table

# View logs
azd logs --service api --tail 100

# Check environment variables
az containerapp show \
  --name ca-taskify-api-dev \
  --resource-group rg-taskify-dev \
  --query properties.template.containers[0].env
```

#### Issue: Database connection timeout

**Solution**:
```bash
# Verify firewall rules
az postgres flexible-server firewall-rule list \
  --resource-group rg-taskify-dev \
  --name psql-taskify-dev-<uniqueId>

# Add firewall rule for Azure services
az postgres flexible-server firewall-rule create \
  --resource-group rg-taskify-dev \
  --name psql-taskify-dev-<uniqueId> \
  --rule-name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

# Test connection
psql "Host=psql-taskify-dev-<uniqueId>.postgres.database.azure.com;Database=taskify;Username=taskifyadmin;Password=$PASSWORD;SSL Mode=Require"
```

#### Issue: GitHub Actions deployment fails with OIDC error

**Solution**:
```bash
# Verify federated credential
az ad app federated-credential list --id "$APP_ID"

# Check subject matches your repository
# Should be: repo:<org>/<repo>:ref:refs/heads/main

# Verify GitHub secrets
gh secret list

# Ensure AZURE_CLIENT_ID, AZURE_TENANT_ID, AZURE_SUBSCRIPTION_ID are set
```

---

## Operational Runbooks

### Runbook 1: Deploy New Application Version

1. **Merge code to main branch** → GitHub Actions auto-deploys to dev
2. **Verify dev deployment**:
   ```bash
   curl https://<api-url>/health
   ```
3. **Promote to production**:
   - Go to GitHub Actions → Azure Deployment workflow
   - Click "Run workflow" → Select "prod" environment
   - Wait for approval (if protection rules enabled)
4. **Monitor deployment**:
   ```bash
   azd logs --service api --environment prod --follow
   ```
5. **Verify production**:
   ```bash
   curl https://<prod-api-url>/health
   ```
6. **Rollback if needed**:
   ```bash
   az containerapp revision deactivate \
     --name <current-revision> \
     --resource-group rg-taskify-prod
   ```

### Runbook 2: Scale for Traffic Spike

**Scenario**: Anticipating 10x traffic increase

```bash
# 1. Update parameter file
"containerAppMinReplicas": { "value": 5 },
"containerAppMaxReplicas": { "value": 50 }

# 2. Apply changes
azd provision --environment prod

# 3. Monitor auto-scaling
az containerapp replica list \
  --name ca-taskify-api-prod \
  --resource-group rg-taskify-prod \
  --output table

# 4. Monitor metrics in Application Insights
# Check: Request rate, response time, CPU/memory usage

# 5. After traffic spike, scale down
"containerAppMinReplicas": { "value": 1 },
"containerAppMaxReplicas": { "value": 30 }
azd provision --environment prod
```

### Runbook 3: Disaster Recovery

**Scenario**: Production database corruption

```bash
# 1. Identify last known-good backup
az postgres flexible-server backup list \
  --resource-group rg-taskify-prod \
  --name psql-taskify-prod-<uniqueId> \
  --output table

# 2. Put application in maintenance mode
az containerapp update \
  --name ca-taskify-web-prod \
  --resource-group rg-taskify-prod \
  --min-replicas 0 --max-replicas 0

# 3. Restore database to new server
az postgres flexible-server restore \
  --resource-group rg-taskify-prod \
  --name psql-taskify-prod-restored \
  --source-server psql-taskify-prod-<uniqueId> \
  --restore-time "<timestamp>"

# 4. Update connection string in Key Vault
NEW_CONNECTION_STRING="Host=psql-taskify-prod-restored.postgres.database.azure.com;..."
az keyvault secret set \
  --vault-name kv-taskify-prod-<uniqueId> \
  --name postgresql-connection-string \
  --value "$NEW_CONNECTION_STRING"

# 5. Restart applications
az containerapp revision restart \
  --name ca-taskify-api-prod-<uniqueId> \
  --resource-group rg-taskify-prod

# 6. Bring web app back online
az containerapp update \
  --name ca-taskify-web-prod \
  --resource-group rg-taskify-prod \
  --min-replicas 1 --max-replicas 30

# 7. Verify functionality
curl https://<prod-api-url>/health
```

---

## Resources

- **Azure Container Apps**: https://learn.microsoft.com/azure/container-apps/
- **PostgreSQL Flexible Server**: https://learn.microsoft.com/azure/postgresql/flexible-server
- **Azure Developer CLI**: https://learn.microsoft.com/azure/developer/azure-developer-cli/
- **Bicep Documentation**: https://learn.microsoft.com/azure/azure-resource-manager/bicep/
- **Application Insights**: https://learn.microsoft.com/azure/azure-monitor/app/app-insights-overview

---

## Support

For deployment issues:
1. Check [Troubleshooting](#troubleshooting) section
2. Review Application Insights logs
3. Consult infrastructure README: [infra/README.md](../infra/README.md)
4. Contact platform engineering team

**Incident Response**: Follow runbooks above for common operational tasks.
