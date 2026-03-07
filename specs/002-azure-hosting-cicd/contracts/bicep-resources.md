# Contract: Azure Bicep Resources

**Phase**: 1 — Design & Contracts  
**Date**: March 6, 2026  
**Plan**: [../plan.md](../plan.md)

---

## Overview

This document defines the contract for Azure infrastructure resources provisioned via Bicep templates. These resources support deploying the Taskify .NET Aspire application to Azure Container Apps with PostgreSQL database, Key Vault secrets management, and Application Insights monitoring.

---

## File Structure

```
infra/
├── main.bicep                    # Root template (orchestrates all modules)
├── main.parameters.json          # Default parameter values
├── main.parameters.dev.json      # Development environment overrides
├── main.parameters.prod.json     # Production environment overrides
├── resources/
│   ├── container-apps.bicep      # Container Apps Environment and Apps
│   ├── postgresql.bicep          # Azure PostgreSQL Flexible Server
│   ├── keyvault.bicep            # Azure Key Vault
│   ├── monitoring.bicep          # Application Insights + Log Analytics
│   └── networking.bicep          # (Optional) VNet for private connectivity
└── hooks/
    ├── predeploy.sh              # Pre-deployment validation
    └── postdeploy.sh             # Post-deployment smoke tests
```

---

## main.bicep

**Purpose**: Root orchestration template that provisions all Azure resources.

**Parameters**:

```bicep
@minLength(1)
@maxLength(20)
@description('Environment name (dev, staging, prod)')
param environmentName string

@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Container App CPU allocation (0.25, 0.5, 1.0, 2.0, 4.0)')
param containerAppCpu string = '0.25'

@description('Container App memory allocation (0.5Gi, 1Gi, 2Gi, 4Gi, 8Gi)')
param containerAppMemory string = '0.5Gi'

@minValue(0)
@maxValue(30)
@description('Minimum replicas for Container Apps (0 = scale to zero)')
param containerAppMinReplicas int = 0

@minValue(1)
@maxValue(30)
@description('Maximum replicas for Container Apps')
param containerAppMaxReplicas int = 10

@description('PostgreSQL SKU name')
@allowed(['Standard_B1ms', 'Standard_B2s', 'Standard_D2s_v3', 'Standard_D4s_v3'])
param postgresqlSkuName string = 'Standard_B1ms'

@description('PostgreSQL tier')
@allowed(['Burstable', 'GeneralPurpose', 'MemoryOptimized'])
param postgresqlTier string = 'Burstable'

@minValue(32)
@maxValue(16384)
@description('PostgreSQL storage size in GB')
param postgresqlStorageGB int = 32

@description('PostgreSQL version')
param postgresqlVersion string = '16'

@description('PostgreSQL high availability mode')
@allowed(['Disabled', 'ZoneRedundant'])
param postgresqlHighAvailability string = 'Disabled'

@description('Container image for Taskify.Api')
param taskifyApiImage string = 'mcr.microsoft.com/dotnet/samples:aspnetapp' // Placeholder

@description('Container image for Taskify.Web')
param taskifyWebImage string = 'mcr.microsoft.com/dotnet/samples:aspnetapp' // Placeholder

@secure()
@description('PostgreSQL administrator password')
param postgresqlAdminPassword string
```

**Outputs**:

```bicep
@description('Container Apps Environment ID')
output containerAppsEnvironmentId string

@description('Taskify API endpoint')
output taskifyApiUrl string

@description('Taskify Web endpoint')
output taskifyWebUrl string

@description('PostgreSQL server FQDN')
output postgresqlServerFqdn string

@description('Key Vault URI')
output keyVaultUri string

@description('Application Insights connection string (secret)')
@secure()
output applicationInsightsConnectionString string
```

**Module References**:

```bicep
module monitoring './resources/monitoring.bicep' = { ... }
module keyVault './resources/keyvault.bicep' = { ... }
module postgresql './resources/postgresql.bicep' = { ... }
module containerApps './resources/container-apps.bicep' = { ... }
```

---

## resources/container-apps.bicep

**Purpose**: Provisions Azure Container Apps Environment and individual Container Apps for API and Web services.

**Parameters**:
- `environmentName`: Environment identifier (dev/prod)
- `location`: Azure region
- `logAnalyticsWorkspaceId`: Log Analytics Workspace resource ID
- `applicationInsightsConnectionString`: App Insights connection string (secret)
- `taskifyApiImage`: Container image for API
- `taskifyWebImage`: Container image for Web
- `cpu`: CPU allocation
- `memory`: Memory allocation
- `minReplicas`: Minimum instance count
- `maxReplicas`: Maximum instance count
- `postgresqlConnectionString`: Database connection string (secret)

**Resources**:

```bicep
resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-taskify-${environmentName}'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsWorkspaceId
      }
    }
  }
}

resource taskifyApiContainerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-taskify-api-${environmentName}'
  location: location
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      secrets: [
        {
          name: 'postgresql-connection-string'
          value: postgresqlConnectionString
        }
        {
          name: 'applicationinsights-connection-string'
          value: applicationInsightsConnectionString
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'taskify-api'
          image: taskifyApiImage
          resources: {
            cpu: json(cpu)
            memory: memory
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: environmentName == 'prod' ? 'Production' : 'Development'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'ConnectionStrings__DefaultConnection'
              secretRef: 'postgresql-connection-string'
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              secretRef: 'applicationinsights-connection-string'
            }
          ]
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: [
          {
            name: 'http-rule'
            http: {
              metadata: {
                concurrentRequests: '100'
              }
            }
          }
        ]
      }
    }
  }
}

resource taskifyWebContainerApp 'Microsoft.App/containerApps@2024-03-01' = {
  // Similar structure to API, adjusted for Blazor Web
  // ...
}
```

**Outputs**:
- `containerAppsEnvironmentId`: Resource ID of Container Apps Environment
- `taskifyApiUrl`: HTTPS URL for API (`ca-taskify-api-{env}.{random}.eastus.azurecontainerapps.io`)
- `taskifyWebUrl`: HTTPS URL for Web (`ca-taskify-web-{env}.{random}.eastus.azurecontainerapps.io`)

---

## resources/postgresql.bicep

**Purpose**: Provisions Azure Database for PostgreSQL Flexible Server.

**Parameters**:
- `environmentName`: Environment identifier
- `location`: Azure region
- `administratorLogin`: Admin username (e.g., `taskifyadmin`)
- `administratorPassword`: Admin password (secure parameter)
- `skuName`: SKU (e.g., `Standard_B1ms`)
- `tier`: Tier (`Burstable`, `GeneralPurpose`, `MemoryOptimized`)
- `storageSizeGB`: Storage allocation
- `version`: PostgreSQL version (default `16`)
- `highAvailabilityMode`: `Disabled` or `ZoneRedundant`

**Resources**:

```bicep
resource postgresqlServer 'Microsoft.DBforPostgreSQL/flexibleServers@2023-12-01-preview' = {
  name: 'psql-taskify-${environmentName}-${uniqueString(resourceGroup().id)}'
  location: location
  sku: {
    name: skuName
    tier: tier
  }
  properties: {
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorPassword
    version: version
    storage: {
      storageSizeGB: storageSizeGB
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: environmentName == 'prod' ? 'Enabled' : 'Disabled'
    }
    highAvailability: {
      mode: highAvailabilityMode
    }
    network: {
      // Public access with firewall rules or VNet integration
      publicNetworkAccess: 'Enabled' // Initially; restrict to VNet later
    }
  }
}

resource postgresqlDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-12-01-preview' = {
  parent: postgresqlServer
  name: 'taskify'
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

resource postgresqlFirewallRule 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2023-12-01-preview' = {
  parent: postgresqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0' // Special range allowing Azure services
  }
}
```

**Outputs**:
- `postgresqlServerFqdn`: Fully qualified domain name (e.g., `psql-taskify-dev-abc123.postgres.database.azure.com`)
- `postgresqlConnectionString`: Connection string for EF Core

**Connection String Format**:
```
Host={fqdn};Database=taskify;Username={adminLogin};Password={adminPassword};SSL Mode=Require;Trust Server Certificate=true
```

---

## resources/keyvault.bicep

**Purpose**: Provisions Azure Key Vault for secrets management.

**Parameters**:
- `environmentName`: Environment identifier
- `location`: Azure region
- `principalId`: Azure AD principal ID (Managed Identity or Service Principal) to grant access

**Resources**:

```bicep
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'kv-taskify-${environmentName}-${uniqueString(resourceGroup().id)}'
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: false // Enable in production
    accessPolicies: [
      {
        tenantId: subscription().tenantId
        objectId: principalId
        permissions: {
          secrets: ['get', 'list']
        }
      }
    ]
  }
}

resource postgresqlConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'postgresql-connection-string'
  properties: {
    value: postgresqlConnectionString // Passed as parameter
  }
}

resource postgresqlAdminPasswordSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'postgresql-admin-password'
  properties: {
    value: postgresqlAdminPassword // Passed as parameter
  }
}

resource applicationInsightsConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'applicationinsights-connection-string'
  properties: {
    value: applicationInsightsConnectionString // Passed as parameter
  }
}
```

**Outputs**:
- `keyVaultUri`: Key Vault URI (e.g., `https://kv-taskify-dev-abc123.vault.azure.net/`)
- `keyVaultName`: Key Vault name

---

## resources/monitoring.bicep

**Purpose**: Provisions Application Insights and Log Analytics Workspace.

**Parameters**:
- `environmentName`: Environment identifier
- `location`: Azure region

**Resources**:

```bicep
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'law-taskify-${environmentName}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 90
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-taskify-${environmentName}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
    RetentionInDays: 90
    SamplingPercentage: 100
  }
}
```

**Outputs**:
- `logAnalyticsWorkspaceId`: Resource ID
- `logAnalyticsWorkspaceCustomerId`: Customer ID for logs
- `applicationInsightsConnectionString`: Connection string (secret)
- `applicationInsightsInstrumentationKey`: Instrumentation key (legacy)

---

## Validation

**Pre-deployment validation** (`infra/hooks/predeploy.sh`):

```bash
#!/bin/bash
set -e

echo "Validating Bicep templates..."
az bicep build --file ./infra/main.bicep

echo "Checking Azure CLI authentication..."
az account show

echo "Pre-deployment validation complete."
```

**Post-deployment validation** (`infra/hooks/postdeploy.sh`):

```bash
#!/bin/bash
set -e

echo "Running smoke tests..."

# Check API health endpoint
API_URL=$(azd env get-values --output json | jq -r '.TASKIFY_API_URL')
curl -f "${API_URL}/health" || { echo "API health check failed"; exit 1; }

# Check Web health endpoint
WEB_URL=$(azd env get-values --output json | jq -r '.TASKIFY_WEB_URL')
curl -f "${WEB_URL}/health" || { echo "Web health check failed"; exit 1; }

echo "Smoke tests passed."
```

---

## Environment-Specific Parameters

### main.parameters.dev.json

```json
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "environmentName": { "value": "dev" },
    "location": { "value": "eastus" },
    "containerAppCpu": { "value": "0.25" },
    "containerAppMemory": { "value": "0.5Gi" },
    "containerAppMinReplicas": { "value": 0 },
    "containerAppMaxReplicas": { "value": 5 },
    "postgresqlSkuName": { "value": "Standard_B1ms" },
    "postgresqlTier": { "value": "Burstable" },
    "postgresqlStorageGB": { "value": 32 },
    "postgresqlHighAvailability": { "value": "Disabled" }
  }
}
```

### main.parameters.prod.json

```json
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "environmentName": { "value": "prod" },
    "location": { "value": "eastus" },
    "containerAppCpu": { "value": "1.0" },
    "containerAppMemory": { "value": "2Gi" },
    "containerAppMinReplicas": { "value": 1 },
    "containerAppMaxReplicas": { "value": 10 },
    "postgresqlSkuName": { "value": "Standard_D2s_v3" },
    "postgresqlTier": { "value": "GeneralPurpose" },
    "postgresqlStorageGB": { "value": 128 },
    "postgresqlHighAvailability": { "value": "ZoneRedundant" }
  }
}
```

---

## Deployment Commands

```bash
# Provision infrastructure only
azd provision --environment dev

# Deploy application only (assumes infrastructure exists)
azd deploy --environment dev

# Provision and deploy (combined)
azd up --environment dev

# Tear down all resources
azd down --environment dev
```

---

## Dependencies

- Azure CLI 2.60+
- Azure Developer CLI (azd) 1.9+
- Bicep CLI (included with Azure CLI)
- Service Principal with Contributor role on subscription (for GitHub Actions)

---

## Compliance

- All resources include tags: `{"environment": "{env}", "project": "taskify"}`
- Naming follows Azure naming conventions and best practices
- Secrets stored in Key Vault, not in Bicep templates
- Soft-delete enabled on Key Vault for disaster recovery
