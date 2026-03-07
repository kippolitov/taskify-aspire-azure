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
@description('Unique identifier suffix to prevent naming conflicts')
param uniqueId string

// === RESOURCES ===

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: 'acrtaskify${environmentName}${uniqueId}'
  location: location
  sku: {
    name: environmentName == 'prod' ? 'Standard' : 'Basic'
  }
  properties: {
    adminUserEnabled: true
    publicNetworkAccess: 'Enabled'
    zoneRedundancy: environmentName == 'prod' ? 'Enabled' : 'Disabled'
  }
}

// === OUTPUTS ===

@description('Container Registry resource ID')
output containerRegistryId string = containerRegistry.id

@description('Container Registry name')
output containerRegistryName string = containerRegistry.name

@description('Container Registry login server')
output containerRegistryLoginServer string = containerRegistry.properties.loginServer

@description('Container Registry endpoint (without https://, azd adds it)')
output containerRegistryEndpoint string = containerRegistry.properties.loginServer

@description('Container Registry admin username')
output containerRegistryUsername string = listCredentials(containerRegistry.id, '2023-11-01-preview').username

@description('Container Registry admin password')
@secure()
output containerRegistryPassword string = listCredentials(containerRegistry.id, '2023-11-01-preview').passwords[0].value
