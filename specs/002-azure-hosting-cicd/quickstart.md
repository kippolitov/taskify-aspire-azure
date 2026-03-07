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

## Infrastructure Updates

When you need to modify Azure infrastructure (change SKUs, add resources, update configurations), follow these steps:

### Update Bicep Templates

1. **Locate the module to modify**:
   ```bash
   # Infrastructure files are in:
   infra/
   ├── main.bicep                    # Root orchestration
   ├── main.parameters.dev.json      # Dev parameters
   ├── main.parameters.prod.json     # Prod parameters
   └── resources/
       ├── monitoring.bicep          # Log Analytics + App Insights
       ├── keyvault.bicep            # Key Vault
       ├── postgresql.bicep          # PostgreSQL database
       ├── container-apps.bicep      # Container Apps
       └── networking.bicep          # VNet (optional)
   ```

2. **Make your changes**:
   ```bash
   # Example: Increase PostgreSQL storage
   # Edit infra/main.parameters.dev.json
   {
     "postgresqlStorageSizeGB": {
       "value": 64  // Changed from 32 to 64
     }
   }
   ```

3. **Validate Bicep syntax**:
   ```bash
   # Lint and validate templates
   az bicep build --file infra/main.bicep
   
   # Check for errors
   az bicep lint --file infra/main.bicep
   ```

4. **Preview changes (What-If)**:
   ```bash
   # See what will change before applying
   az deployment group what-if \
     --resource-group rg-taskify-dev \
     --template-file infra/main.bicep \
     --parameters infra/main.parameters.dev.json
   ```

5. **Apply infrastructure updates**:
   ```bash
   # Using azd (recommended)
   azd provision
   
   # Or using Azure CLI directly
   az deployment group create \
     --resource-group rg-taskify-dev \
     --template-file infra/main.bicep \
     --parameters infra/main.parameters.dev.json
   ```

### Common Infrastructure Changes

#### Scale Container Apps

```bash
# Update parameters file
{
  "containerAppCpu": { "value": 0.5 },  // Increase from 0.25
  "containerAppMemory": { "value": "1Gi" },  // Increase from 0.5Gi
  "containerAppMaxReplicas": { "value": 20 }  // Increase from 10
}

# Redeploy
azd provision
```

#### Upgrade PostgreSQL SKU

```bash
# Update parameters file
{
  "postgresqlSkuName": { "value": "Standard_D2s_v3" },  // Upgrade from B1ms
  "postgresqlTier": { "value": "GeneralPurpose" },  // Change from Burstable
  "postgresqlStorageSizeGB": { "value": 128 }  // Increase storage
}

# Redeploy (minimal downtime, ~2-5 minutes)
azd provision
```

#### Enable High Availability

```bash
# Update parameters file
{
  "postgresqlHighAvailabilityMode": { "value": "ZoneRedundant" }  // Enable HA
}

# Redeploy (creates standby replica)
azd provision
```

#### Add VNet Integration

```bash
# Update main.bicep parameters
{
  "enableVNetIntegration": { "value": true }
}

# Provision networking resources
azd provision
```

### Infrastructure Update Best Practices

1. **Always test in development first**:
   ```bash
   azd env select dev
   azd provision
   # Test thoroughly, then apply to production
   ```

2. **Use What-If to preview changes**:
   ```bash
   az deployment group what-if \
     --resource-group rg-taskify-prod \
     --template-file infra/main.bicep \
     --parameters infra/main.parameters.prod.json
   ```

3. **Backup critical data before major changes**:
   ```bash
   # Create PostgreSQL backup
   az postgres flexible-server backup create \
     --resource-group rg-taskify-prod \
     --name psql-taskify-prod-<uniqueId> \
     --backup-name pre-upgrade-$(date +%Y%m%d)
   ```

4. **Monitor deployments**:
   ```bash
   # Watch deployment progress
   az deployment group list \
     --resource-group rg-taskify-dev \
     --output table
   ```

5. **Rollback infrastructure changes**:
   ```bash
   # Revert parameters to previous values
   git checkout HEAD~1 -- infra/main.parameters.dev.json
   
   # Redeploy previous configuration
   azd provision
   ```

### Using PowerShell Deployment Scripts

For complex deployments with orchestration:

```bash
# Deploy with infrastructure validation
./scripts/deploy-to-azure.ps1 -Environment dev -ProvisionOnly

# Deploy with database migrations
./scripts/deploy-to-azure.ps1 -Environment dev

# Skip migrations (infrastructure only)
./scripts/deploy-to-azure.ps1 -Environment prod -SkipMigrations

# Dry-run to validate without deploying
./scripts/deploy-to-azure.ps1 -Environment dev -DryRun
```

See [infra/README.md](../../infra/README.md) for complete Bicep module documentation.

---

## Resource Cleanup

To delete Azure resources and avoid ongoing charges, follow these procedures based on your cleanup scope.

### Complete Environment Cleanup

Remove all resources for a specific environment:

```bash
# Using azd (recommended - safest)
azd down

# Confirms resource deletion
# Prompts: "Are you sure you want to delete all resources? (y/N)"
# Type 'y' and press Enter
```

**What gets deleted**:
- Resource Group (`rg-taskify-dev-<uniqueId>`)
- All resources inside (Container Apps, PostgreSQL, Key Vault, etc.)
- Local `.azure/<env>` directory

**Retention**:
- Container App revisions: Deleted immediately
- PostgreSQL backups: Retained for configured period (7-35 days)
- Key Vault: Soft-deleted (90-day recovery window)
- Application Insights data: Retained based on workspace policy

### Delete Specific Resource Group

If you created resources outside azd, delete manually:

```bash
# Delete resource group (WARNING: irreversible)
az group delete --name rg-taskify-dev --yes --no-wait

# Verify deletion
az group list --query "[?name=='rg-taskify-dev']" --output table
```

### Purge Soft-Deleted Resources

Key Vault uses soft-delete by default. To permanently delete:

```bash
# List soft-deleted Key Vaults
az keyvault list-deleted --query "[?name contains(@, 'taskify')]"

# Purge Key Vault (permanent, cannot be recovered)
az keyvault purge --name kv-taskify-dev-<uniqueId>

# Note: Only resource group owners can purge soft-deleted vaults
```

### Selective Resource Cleanup

Delete individual resources while keeping others:

```bash
# Delete Container Apps only
az containerapp delete \
  --name ca-taskify-api-dev-<uniqueId> \
  --resource-group rg-taskify-dev-<uniqueId> \
  --yes

az containerapp delete \
  --name ca-taskify-web-dev-<uniqueId> \
  --resource-group rg-taskify-dev-<uniqueId> \
  --yes

# Delete PostgreSQL database only
az postgres flexible-server delete \
  --resource-group rg-taskify-dev-<uniqueId> \
  --name psql-taskify-dev-<uniqueId> \
  --yes

# Note: Monitor costs after partial cleanup to ensure no hidden charges
```

### Verify Complete Cleanup

```bash
# Check for remaining resources
az resource list --resource-group rg-taskify-dev --output table

# Check for remaining resource groups
az group list --query "[?contains(name, 'taskify')]" --output table

# Check for soft-deleted resources
az keyvault list-deleted --query "[?name contains(@, 'taskify')]"
```

### Cost Verification After Cleanup

```bash
# Wait 24 hours, then check Azure Cost Management
az consumption usage list \
  --start-date $(date -u -d "yesterday" '+%Y-%m-%d') \
  --end-date $(date -u '+%Y-%m-%d') \
  --query "[?contains(instanceName, 'taskify')]"

# Should return empty or minimal charges from last day
```

### Cleanup Checklist

Before deleting resources, ensure:
- ✅ Data is backed up (if needed)
- ✅ Users are notified (if production environment)
- ✅ DNS records are updated (if custom domain configured)
- ✅ GitHub Actions secrets are removed (if decommissioning entirely)
- ✅ Billing alerts are disabled
- ✅ Service Principal credentials are revoked (if no longer needed)

### Emergency Stop (Cost Containment)

If you need to immediately stop charges without deleting resources:

```bash
# Stop Container Apps (scale to zero)
az containerapp update \
  --name ca-taskify-api-dev-<uniqueId> \
  --resource-group rg-taskify-dev-<uniqueId> \
  --min-replicas 0 \
  --max-replicas 0

az containerapp update \
  --name ca-taskify-web-dev-<uniqueId> \
  --resource-group rg-taskify-dev-<uniqueId> \
  --min-replicas 0 \
  --max-replicas 0

# Stop PostgreSQL (not recommended for production)
az postgres flexible-server stop \
  --resource-group rg-taskify-dev-<uniqueId> \
  --name psql-taskify-dev-<uniqueId>

# Note: Stopped PostgreSQL servers still incur storage charges
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

## Rollback and Revision Management

Azure Container Apps maintains revision history, allowing you to rollback to previous working versions if issues are detected.

### Understanding Container App Revisions

Each deployment creates a new **revision** (immutable snapshot):
- Revisions are named: `ca-taskify-api-dev--<revision-suffix>`
- Up to 100 revisions retained
- Revisions can run side-by-side for blue-green deployments

### View Current Revisions

```bash
# List all revisions for API
az containerapp revision list \
  --name ca-taskify-api-dev-<uniqueId> \
  --resource-group rg-taskify-dev-<uniqueId> \
  --output table

# List all revisions for Web
az containerapp revision list \
  --name ca-taskify-web-dev-<uniqueId> \
  --resource-group rg-taskify-dev-<uniqueId> \
  --output table
```

**Output columns**:
- `Name`: Revision identifier
- `Active`: Currently receiving traffic
- `Created`: Deployment timestamp
- `TrafficWeight`: % of traffic routed to this revision

### Rollback to Previous Revision

If the latest deployment has issues, rollback to a known-good revision:

```bash
# Step 1: Identify the previous working revision
az containerapp revision list \
  --name ca-taskify-api-dev-<uniqueId> \
  --resource-group rg-taskify-dev-<uniqueId> \
  --query "[?active==\`true\`].{Name:name, Created:properties.createdTime}" \
  --output table

# Step 2: Deactivate the problematic current revision
# (Container Apps will automatically activate the previous revision)
az containerapp revision deactivate \
  --name <current-revision-name> \
  --resource-group rg-taskify-dev-<uniqueId>

# Step 3: Verify traffic shifted to previous revision
az containerapp revision list \
  --name ca-taskify-api-dev-<uniqueId> \
  --resource-group rg-taskify-dev-<uniqueId> \
  --query "[?trafficWeight!=null].{Name:name, Traffic:trafficWeight}" \
  --output table
```

### Manual Traffic Splitting (Blue-Green Deployment)

For zero-downtime rollback, split traffic between revisions:

```bash
# Route 90% traffic to previous revision, 10% to new (canary)
az containerapp ingress traffic set \
  --name ca-taskify-api-dev-<uniqueId> \
  --resource-group rg-taskify-dev-<uniqueId> \
  --revision-weight <previous-revision>=90 <current-revision>=10

# Monitor canary for errors, then route 100% to working revision
az containerapp ingress traffic set \
  --name ca-taskify-api-dev-<uniqueId> \
  --resource-group rg-taskify-dev-<uniqueId> \
  --revision-weight <working-revision>=100
```

### Set Single Revision Mode

Force Container Apps to use only one active revision (automatic rollback):

```bash
# Set single revision mode
az containerapp revision set-mode \
  --name ca-taskify-api-dev-<uniqueId> \
  --resource-group rg-taskify-dev-<uniqueId> \
  --mode single

# Activate specific revision
az containerapp revision activate \
  --name <revision-name> \
  --resource-group rg-taskify-dev-<uniqueId>
```

### Automated Rollback in CI/CD

For GitHub Actions workflows, rollback is triggered if smoke tests fail:

```yaml
- name: Rollback on failure
  if: failure()
  run: |
    echo "Deployment failed. Initiating rollback..."
    PREVIOUS_REVISION=$(az containerapp revision list \
      --name ca-taskify-api-dev-<uniqueId> \
      --resource-group rg-taskify-dev-<uniqueId> \
      --query "[?properties.provisioningState=='Succeeded'] | sort_by(@, &properties.createdTime) | [-2].name" \
      -o tsv)
    
    az containerapp revision activate \
      --name $PREVIOUS_REVISION \
      --resource-group rg-taskify-dev-<uniqueId>
```

### Database Migration Rollback

**CAUTION**: EF Core migrations are forward-only by default. Rollback requires:

1. **Manual SQL rollback script**:
   ```bash
   # Connect to database
   psql "$(azd env get-values --output json | jq -r '.POSTGRESQL_CONNECTION_STRING')"
   
   # Execute rollback SQL
   \i migrations/rollback_<migration-name>.sql
   ```

2. **EF Core migration revert** (local only):
   ```bash
   # Revert to previous migration
   dotnet ef migrations remove --project src/Taskify.Api
   ```

3. **Best practice**: Test migrations in staging first, use transactional DDL where possible.

### Rollback Playbook

**If deployment fails in CI/CD**:
1. GitHub Actions automatically deactivates failing revision (if smoke tests fail)
2. Check logs: `azd logs --service api --follow`
3. Verify previous revision activated: `az containerapp revision list`

**If issue discovered post-deployment**:
1. Identify last known-good revision: `az containerapp revision list`
2. Deactivate current revision: `az containerapp revision deactivate`
3. Monitor Application Insights for errors
4. If database migration issue, manually rollback schema changes
5. Re-deploy fix via `azd deploy` or GitHub Actions

**Recovery time objective (RTO)**: <5 minutes for application rollback, <30 minutes for database rollback

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
