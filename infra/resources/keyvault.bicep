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

@description('Azure AD principal ID (Managed Identity or Service Principal) to grant Key Vault access')
param principalId string

@secure()
@description('PostgreSQL connection string')
param postgresqlConnectionString string

@secure()
@description('PostgreSQL administrator password')
param postgresqlAdminPassword string

@secure()
@description('Application Insights connection string')
param applicationInsightsConnectionString string

// === RESOURCES ===

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'kv-taskify-${environmentName}-${uniqueId}'
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
    enablePurgeProtection: environmentName == 'prod' ? true : false
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
    value: postgresqlConnectionString
  }
}

resource postgresqlAdminPasswordSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'postgresql-admin-password'
  properties: {
    value: postgresqlAdminPassword
  }
}

resource applicationInsightsConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'applicationinsights-connection-string'
  properties: {
    value: applicationInsightsConnectionString
  }
}

// === OUTPUTS ===

@description('Key Vault URI')
output keyVaultUri string = 'https://${keyVault.name}.${environment().suffixes.keyvaultDns}/'

@description('Key Vault name')
output keyVaultName string = keyVault.name

@description('Key Vault resource ID')
output keyVaultId string = keyVault.id
