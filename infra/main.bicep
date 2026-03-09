targetScope = 'resourceGroup'

// === PARAMETERS ===

@minLength(1)
@maxLength(20)
@description('Environment name (dev, prod)')
param environmentName string

@description('Azure region for all resources')
param location string = resourceGroup().location

@minLength(3)
@maxLength(8)
@description('Unique identifier suffix to prevent naming conflicts (generated from subscription ID hash or timestamp)')
param uniqueId string = substring(uniqueString(resourceGroup().id), 0, 8)

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

@minValue(7)
@maxValue(35)
@description('PostgreSQL backup retention days')
param postgresqlBackupRetentionDays int = 7

@description('Container image for Taskify.Api')
param taskifyApiImage string = 'mcr.microsoft.com/dotnet/samples:aspnetapp' // Placeholder

@description('Container image for Taskify.Web')
param taskifyWebImage string = 'mcr.microsoft.com/dotnet/samples:aspnetapp' // Placeholder

@secure()
@description('PostgreSQL administrator password')
param postgresqlAdminPassword string

@description('Enable VNet integration (recommended for production)')
param enableVNetIntegration bool = false

// === MODULE REFERENCES ===

// Optional: Networking module (VNet integration for enhanced security)
module networking './resources/networking.bicep' = if (enableVNetIntegration) {
  name: 'networking'
  params: {
    environmentName: environmentName
    location: location
    uniqueId: uniqueId
  }
}

module monitoring './resources/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    environmentName: environmentName
    location: location
    uniqueId: uniqueId
  }
}

module containerRegistry './resources/acr.bicep' = {
  name: 'containerRegistry'
  params: {
    environmentName: environmentName
    location: location
    uniqueId: uniqueId
  }
}

module postgresql './resources/postgresql.bicep' = {
  name: 'postgresql'
  params: {
    environmentName: environmentName
    location: location
    uniqueId: uniqueId
    administratorLogin: 'taskifyadmin'
    administratorPassword: postgresqlAdminPassword
    skuName: postgresqlSkuName
    tier: postgresqlTier
    storageSizeGB: postgresqlStorageGB
    version: postgresqlVersion
    highAvailabilityMode: postgresqlHighAvailability
    backupRetentionDays: postgresqlBackupRetentionDays
  }
}

module containerApps './resources/container-apps.bicep' = {
  name: 'containerApps'
  params: {
    environmentName: environmentName
    location: location
    uniqueId: uniqueId
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
    logAnalyticsWorkspaceCustomerId: monitoring.outputs.logAnalyticsWorkspaceCustomerId
    applicationInsightsConnectionString: monitoring.outputs.applicationInsightsConnectionString
    taskifyApiImage: taskifyApiImage
    taskifyWebImage: taskifyWebImage
    cpu: containerAppCpu
    memory: containerAppMemory
    minReplicas: containerAppMinReplicas
    maxReplicas: containerAppMaxReplicas
    postgresqlConnectionString: postgresql.outputs.postgresqlConnectionString
    containerRegistryLoginServer: containerRegistry.outputs.containerRegistryLoginServer
    containerRegistryUsername: containerRegistry.outputs.containerRegistryUsername
    containerRegistryPassword: containerRegistry.outputs.containerRegistryPassword
  }
}

// Key Vault module - temporarily disabled to troubleshoot deployment validation issues
// TODO: Re-enable after initial deployment succeeds
/*
module keyVault './resources/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    environmentName: environmentName
    location: location
    uniqueId: uniqueId
    principalId: containerApps.outputs.apiManagedIdentityPrincipalId
    postgresqlConnectionString: postgresql.outputs.postgresqlConnectionString
    postgresqlAdminPassword: postgresqlAdminPassword
    applicationInsightsConnectionString: monitoring.outputs.applicationInsightsConnectionString
  }
}
*/

// === OUTPUTS ===

@description('Container Apps Environment ID')
output containerAppsEnvironmentId string = containerApps.outputs.containerAppsEnvironmentId

@description('Taskify API endpoint')
output taskifyApiUrl string = containerApps.outputs.taskifyApiUrl

@description('Taskify Web endpoint')
output taskifyWebUrl string = containerApps.outputs.taskifyWebUrl

@description('PostgreSQL server FQDN')
output postgresqlServerFqdn string = postgresql.outputs.postgresqlServerFqdn

@description('PostgreSQL database name')
output postgresqlDatabaseName string = postgresql.outputs.postgresqlDatabaseName

@description('PostgreSQL connection string (for migrations)')
@secure()
output POSTGRESQL_CONNECTION_STRING string = postgresql.outputs.postgresqlConnectionString

// Temporarily disabled - re-enable when Key Vault module is uncommented
// @description('Key Vault URI')
// output keyVaultUri string = keyVault.outputs.keyVaultUri

@description('Application Insights connection string (secret)')
@secure()
output applicationInsightsConnectionString string = monitoring.outputs.applicationInsightsConnectionString

// === CONTAINER REGISTRY OUTPUTS ===

@description('Azure Container Registry endpoint (for azd)')
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = containerRegistry.outputs.containerRegistryEndpoint

@description('Azure Container Registry name')
output AZURE_CONTAINER_REGISTRY_NAME string = containerRegistry.outputs.containerRegistryName

// === SERVICE OUTPUTS (for azd) ===

@description('API service endpoint')
output SERVICE_API_ENDPOINT_URL string = containerApps.outputs.taskifyApiUrl

@description('Web service endpoint')
output SERVICE_WEB_ENDPOINT_URL string = containerApps.outputs.taskifyWebUrl
