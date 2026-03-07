# Infrastructure as Code - Bicep Templates

This directory contains Azure Bicep templates for deploying the Taskify application infrastructure to Azure.

## Directory Structure

```
infra/
├── main.bicep                    # Root orchestration template
├── main.parameters.json          # Default parameter values (template)
├── main.parameters.dev.json      # Development environment overrides
├── main.parameters.prod.json     # Production environment overrides
├── resources/                    # Bicep modules
│   ├── monitoring.bicep          # Log Analytics + Application Insights
│   ├── keyvault.bicep            # Azure Key Vault
│   ├── postgresql.bicep          # PostgreSQL Flexible Server
│   ├── container-apps.bicep      # Container Apps Environment + Apps
│   └── networking.bicep          # (Optional) VNet configuration
└── hooks/                        # Deployment lifecycle scripts
    ├── predeploy.sh              # Pre-deployment validation
    └── postdeploy.sh             # Post-deployment smoke tests
```

---

## Architecture Overview

The infrastructure consists of the following Azure resources:

### Core Services
- **Azure Container Apps**: Serverless compute for API and Web applications
- **Azure Database for PostgreSQL**: Managed database service
- **Azure Key Vault**: Secrets management (connection strings, passwords)
- **Application Insights**: Application performance monitoring
- **Log Analytics Workspace**: Centralized logging

### Optional Services
- **Virtual Network**: Private network isolation (production environments)
- **Network Security Groups**: Traffic filtering rules

---

## Usage

### Deploy with Azure Developer CLI (Recommended)

```bash
# Initialize azd environment
azd init

# Deploy everything (infrastructure + applications)
azd up

# Or deploy step-by-step:
azd provision  # Infrastructure only
azd deploy     # Applications only
```

### Deploy with Azure CLI

```bash
# Login to Azure
az login

# Create resource group
az group create --name rg-taskify-dev --location eastus

# Deploy infrastructure
az deployment group create \
  --resource-group rg-taskify-dev \
  --template-file infra/main.bicep \
  --parameters infra/main.parameters.dev.json \
  --parameters postgresqlAdminPassword="<secure-password>"

# Get deployment outputs
az deployment group show \
  --resource-group rg-taskify-dev \
  --name main \
  --query properties.outputs
```

---

## Parameter Files

### Default Parameters (`main.parameters.json`)

Template parameter file with placeholder values. **Do not use directly** - override with environment-specific files.

### Development (`main.parameters.dev.json`)

Optimized for cost-effective development:
- **Container Apps**: 0.25 vCPU, 0.5 GiB memory, scale-to-zero enabled
- **PostgreSQL**: Burstable tier (Standard_B1ms), 32 GB storage, no HA
- **Backup**: 7-day retention, no geo-redundancy
- **Estimated cost**: ~$18-25/month

### Production (`main.parameters.prod.json`)

Optimized for reliability and performance:
- **Container Apps**: 1.0 vCPU, 2 GiB memory, min 1 replica
- **PostgreSQL**: General Purpose tier (Standard_D2s_v3), 128 GB storage, zone-redundant HA
- **Backup**: 35-day retention, geo-redundant enabled
- **Estimated cost**: ~$370/month

---

## Customization

### Override Parameters

You can override any parameter at deployment time:

**With azd**:
```bash
azd provision --parameters containerAppCpu=0.5 --parameters containerAppMemory=1Gi
```

**With Azure CLI**:
```bash
az deployment group create \
  --template-file infra/main.bicep \
  --parameters infra/main.parameters.dev.json \
  --parameters containerAppCpu=0.5 \
  --parameters containerAppMemory=1Gi
```

### Enable VNet Integration

For production deployments with private networking:

```bash
azd provision --parameters enableVNetIntegration=true
```

This deploys:
- Virtual Network with dedicated subnets
- Network Security Groups with firewall rules
- VNet-integrated Container Apps Environment
- PostgreSQL with VNet integration (no public access)

---

## Module Documentation

### `resources/monitoring.bicep`

**Deploys**: Log Analytics Workspace, Application Insights

**Parameters**:
- `environmentName`: Environment identifier (dev/prod)
- `location`: Azure region
- `uniqueId`: Unique suffix for resource names

**Outputs**:
- `logAnalyticsWorkspaceId`: Workspace resource ID
- `applicationInsightsConnectionString`: Connection string for apps

**Cost**: ~$5-10/month (depends on ingestion volume)

---

### `resources/keyvault.bicep`

**Deploys**: Azure Key Vault with secrets

**Parameters**:
- `principalId`: Managed Identity ID for access policies
- `postgresqlConnectionString`: Database connection (stored as secret)
- `postgresqlAdminPassword`: Password (stored as secret)
- `applicationInsightsConnectionString`: App Insights connection (stored as secret)

**Outputs**:
- `keyVaultUri`: Vault URI (e.g., `https://kv-taskify-dev.vault.azure.net/`)

**Cost**: ~$0.03/10,000 operations

---

### `resources/postgresql.bicep`

**Deploys**: Azure Database for PostgreSQL Flexible Server

**Parameters**:
- `administratorLogin`: Admin username
- `administratorPassword`: Admin password (secure)
- `skuName`: SKU (e.g., `Standard_B1ms`)
- `tier`: Tier (`Burstable`, `GeneralPurpose`, `MemoryOptimized`)
- `storageSizeGB`: Storage allocation (32-16384 GB)
- `highAvailabilityMode`: `Disabled` or `ZoneRedundant`

**Outputs**:
- `postgresqlServerFqdn`: Server FQDN
- `postgresqlConnectionString`: Connection string for EF Core

**Cost**: 
- Dev (Burstable B1ms): ~$12-15/month
- Prod (GP D2s_v3 with HA): ~$250-300/month

---

### `resources/container-apps.bicep`

**Deploys**: Container Apps Environment, API Container App, Web Container App

**Parameters**:
- `taskifyApiImage`: Container image for API
- `taskifyWebImage`: Container image for Web
- `cpu`: CPU allocation (0.25-4.0 vCPU)
- `memory`: Memory allocation (0.5Gi-8Gi)
- `minReplicas`: Minimum instance count (0 = scale-to-zero)
- `maxReplicas`: Maximum instance count

**Outputs**:
- `taskifyApiUrl`: API HTTPS endpoint
- `taskifyWebUrl`: Web HTTPS endpoint

**Cost**:
- Dev (scale-to-zero): ~$0-5/month (only when running)
- Prod (min 1 replica): ~$50-80/month

---

### `resources/networking.bicep` (Optional)

**Deploys**: Virtual Network, Subnets, Network Security Groups

**Parameters**:
- `vnetAddressPrefix`: VNet CIDR (default: `10.0.0.0/16`)
- `containerAppsSubnetPrefix`: Subnet for Container Apps (default: `10.0.0.0/23`)
- `postgresqlSubnetPrefix`: Subnet for PostgreSQL (default: `10.0.2.0/24`)

**Outputs**:
- `vnetId`: Virtual Network resource ID
- `containerAppsSubnetId`: Subnet ID for Container Apps
- `postgresqlSubnetId`: Subnet ID for PostgreSQL

**Cost**: ~$0-5/month (minimal for VNet itself)

---

## Validation

### Validate Bicep Templates

```bash
# Validate main template
az bicep build --file infra/main.bicep

# Validate all modules
az bicep build --file infra/resources/monitoring.bicep
az bicep build --file infra/resources/keyvault.bicep
az bicep build --file infra/resources/postgresql.bicep
az bicep build --file infra/resources/container-apps.bicep
az bicep build --file infra/resources/networking.bicep
```

### Lint for Best Practices

```bash
# Lint main template
az bicep lint --file infra/main.bicep

# Fix auto-fixable issues
az bicep format --file infra/main.bicep
```

### What-If Deployment

Preview changes before applying:

```bash
az deployment group what-if \
  --resource-group rg-taskify-dev \
  --template-file infra/main.bicep \
  --parameters infra/main.parameters.dev.json
```

---

## Troubleshooting

### Issue: Resource Name Conflicts

**Symptom**: Deployment fails with "Resource name already exists"

**Solution**: The templates use `uniqueString(resourceGroup().id)` to generate unique suffixes. If you're redeploying:
1. Delete the old resource group: `az group delete --name rg-taskify-dev`
2. Or change the `uniqueId` parameter manually

---

### Issue: PostgreSQL Connection Timeout

**Symptom**: Applications can't connect to PostgreSQL

**Possible causes**:
1. Firewall rules not configured (check `AllowAzureServices` rule exists)
2. VNet integration enabled but subnet not delegated
3. PostgreSQL server not fully started (wait 5-10 minutes after provisioning)

**Solution**:
```bash
# Verify firewall rules
az postgres flexible-server firewall-rule list \
  --resource-group rg-taskify-dev \
  --name psql-taskify-dev-<uniqueId>

# Test connection from local machine
psql "Host=psql-taskify-dev-<uniqueId>.postgres.database.azure.com;Database=taskify;Username=taskifyadmin;Password=<password>;SSL Mode=Require"
```

---

### Issue: Container Apps Not Accessible

**Symptom**: HTTPS URLs return 503 or timeout

**Possible causes**:
1. Container images not built/pushed yet (run `azd deploy`)
2. Container Apps still scaling from zero (wait 30-60 seconds)
3. Application startup failure (check logs)

**Solution**:
```bash
# Check revision status
az containerapp revision list \
  --name ca-taskify-api-dev-<uniqueId> \
  --resource-group rg-taskify-dev-<uniqueId>

# View logs
azd logs --service api --follow
```

---

## Best Practices

1. **Use parameter files**: Never hardcode values in `main.bicep`
2. **Secure secrets**: Store passwords in Azure Key Vault, not parameter files
3. **Tag resources**: Add tags for cost tracking and governance
4. **Enable diagnostics**: Send logs to Log Analytics for troubleshooting
5. **Test in dev first**: Validate changes in development before production
6. **Document dependencies**: Use comments in Bicep to explain module relationships
7. **Version control**: Commit all Bicep files and parameter files (except secrets)

---

## Resources

- [Azure Bicep Documentation](https://learn.microsoft.com/azure/azure-resource-manager/bicep/)
- [Bicep Best Practices](https://learn.microsoft.com/azure/azure-resource-manager/bicep/best-practices)
- [Azure Container Apps Bicep Reference](https://learn.microsoft.com/azure/templates/microsoft.app/containerapps)
- [PostgreSQL Flexible Server Bicep Reference](https://learn.microsoft.com/azure/templates/microsoft.dbforpostgresql/flexibleservers)
- [Azure Developer CLI (azd)](https://learn.microsoft.com/azure/developer/azure-developer-cli/)
