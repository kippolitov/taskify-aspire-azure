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

@description('PostgreSQL administrator username')
param administratorLogin string = 'taskifyadmin'

@secure()
@description('PostgreSQL administrator password')
param administratorPassword string

@description('PostgreSQL SKU name')
@allowed(['Standard_B1ms', 'Standard_B2s', 'Standard_D2s_v3', 'Standard_D4s_v3'])
param skuName string = 'Standard_B1ms'

@description('PostgreSQL tier')
@allowed(['Burstable', 'GeneralPurpose', 'MemoryOptimized'])
param tier string = 'Burstable'

@minValue(32)
@maxValue(16384)
@description('PostgreSQL storage size in GB')
param storageSizeGB int = 32

@description('PostgreSQL version')
param version string = '16'

@description('PostgreSQL high availability mode')
@allowed(['Disabled', 'ZoneRedundant'])
param highAvailabilityMode string = 'Disabled'

@minValue(7)
@maxValue(35)
@description('PostgreSQL backup retention days')
param backupRetentionDays int = 7

// === RESOURCES ===

resource postgresqlServer 'Microsoft.DBforPostgreSQL/flexibleServers@2023-12-01-preview' = {
  name: 'psql-taskify-${environmentName}-${uniqueId}'
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
      backupRetentionDays: backupRetentionDays
      geoRedundantBackup: environmentName == 'prod' ? 'Enabled' : 'Disabled'
    }
    highAvailability: {
      mode: highAvailabilityMode
    }
    network: {
      // Public access with firewall rules (restrict to VNet in production)
      publicNetworkAccess: 'Enabled'
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

// === OUTPUTS ===

@description('PostgreSQL server FQDN')
output postgresqlServerFqdn string = postgresqlServer.properties.fullyQualifiedDomainName

@description('PostgreSQL server name')
output postgresqlServerName string = postgresqlServer.name

@description('PostgreSQL database name')
output postgresqlDatabaseName string = postgresqlDatabase.name

@description('PostgreSQL connection string for EF Core')
@secure()
output postgresqlConnectionString string = 'Host=${postgresqlServer.properties.fullyQualifiedDomainName};Database=${postgresqlDatabase.name};Username=${administratorLogin};Password=${administratorPassword};SSL Mode=Require;Trust Server Certificate=true'
