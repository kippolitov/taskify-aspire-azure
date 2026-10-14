targetScope = 'resourceGroup'

// Azure Managed Redis, used only as the SignalR backplane between API replicas
// (pub/sub, no persisted data). See docs/adr/0003-redis-signalr-backplane.md.

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

@description('Two nodes with high availability (prod) or one node (dev)')
param highAvailability bool = false

// === RESOURCES ===

resource redisCluster 'Microsoft.Cache/redisEnterprise@2025-07-01' = {
  name: 'redis-${environmentName}-${uniqueId}'
  location: location
  sku: {
    name: 'Balanced_B0'
  }
  properties: {
    highAvailability: highAvailability ? 'Enabled' : 'Disabled'
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource redisDatabase 'Microsoft.Cache/redisEnterprise/databases@2025-07-01' = {
  parent: redisCluster
  name: 'default'
  properties: {
    clientProtocol: 'Encrypted'
    port: 10000
    clusteringPolicy: 'NoCluster'
    evictionPolicy: 'NoEviction'
    // Access keys are off by default on Azure Managed Redis; the backplane uses one.
    accessKeysAuthentication: 'Enabled'
  }
}

// === OUTPUTS ===

@description('Redis host name')
output hostName string = redisCluster.properties.hostName

@description('StackExchange.Redis connection string for the SignalR backplane')
@secure()
output connectionString string = '${redisCluster.properties.hostName}:10000,password=${redisDatabase.listKeys().primaryKey},ssl=True,abortConnect=False'
