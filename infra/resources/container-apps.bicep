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

@description('Log Analytics Workspace resource ID')
param logAnalyticsWorkspaceId string

@description('Log Analytics Workspace customer ID')
param logAnalyticsWorkspaceCustomerId string

@secure()
@description('Application Insights connection string')
param applicationInsightsConnectionString string

@description('Container image for Taskify.Api')
param taskifyApiImage string

@description('Container image for Taskify.Web')
param taskifyWebImage string

@description('Container App CPU allocation (0.25, 0.5, 1.0, 2.0, 4.0)')
param cpu string = '0.25'

@description('Container App memory allocation (0.5Gi, 1Gi, 2Gi, 4Gi, 8Gi)')
param memory string = '0.5Gi'

@minValue(0)
@maxValue(30)
@description('Minimum replicas for Container Apps (0 = scale to zero)')
param minReplicas int = 0

@minValue(1)
@maxValue(30)
@description('Maximum replicas for Container Apps')
param maxReplicas int = 10

@secure()
@description('PostgreSQL connection string')
param postgresqlConnectionString string

@description('Azure Container Registry login server')
param containerRegistryLoginServer string

@secure()
@description('Azure Container Registry admin username')
param containerRegistryUsername string

@secure()
@description('Azure Container Registry admin password')
param containerRegistryPassword string

// === RESOURCES ===

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-taskify-${environmentName}-${uniqueId}'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsWorkspaceCustomerId
        sharedKey: listKeys(logAnalyticsWorkspaceId, '2023-09-01').primarySharedKey
      }
    }
    zoneRedundant: environmentName == 'prod' ? true : false
  }
}

resource taskifyApiContainerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-taskify-api-${environmentName}-${uniqueId}'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  tags: {
    'azd-service-name': 'api'
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        {
          server: containerRegistryLoginServer
          username: containerRegistryUsername
          passwordSecretRef: 'acr-password'
        }
      ]
      secrets: [
        {
          name: 'acr-password'
          value: containerRegistryPassword
        }
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
              name: 'ConnectionStrings__taskifydb'
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
  name: 'ca-taskify-web-${environmentName}-${uniqueId}'
  location: location
  tags: {
    'azd-service-name': 'web'
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        {
          server: containerRegistryLoginServer
          username: containerRegistryUsername
          passwordSecretRef: 'acr-password'
        }
      ]
      secrets: [
        {
          name: 'acr-password'
          value: containerRegistryPassword
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
          name: 'taskify-web'
          image: taskifyWebImage
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
              name: 'services__taskify-api__http__0'
              value: 'https://${taskifyApiContainerApp.properties.configuration.ingress.fqdn}'
            }
            {
              name: 'services__taskify-api__https__0'
              value: 'https://${taskifyApiContainerApp.properties.configuration.ingress.fqdn}'
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

// === OUTPUTS ===

@description('Container Apps Environment resource ID')
output containerAppsEnvironmentId string = containerAppsEnvironment.id

@description('Container Apps Environment name')
output containerAppsEnvironmentName string = containerAppsEnvironment.name

@description('Taskify API Managed Identity Principal ID')
output apiManagedIdentityPrincipalId string = taskifyApiContainerApp.identity.principalId

@description('Taskify API FQDN')
output taskifyApiFqdn string = taskifyApiContainerApp.properties.configuration.ingress.fqdn

@description('Taskify API URL')
output taskifyApiUrl string = 'https://${taskifyApiContainerApp.properties.configuration.ingress.fqdn}'

@description('Taskify Web FQDN')
output taskifyWebFqdn string = taskifyWebContainerApp.properties.configuration.ingress.fqdn

@description('Taskify Web URL')
output taskifyWebUrl string = 'https://${taskifyWebContainerApp.properties.configuration.ingress.fqdn}'
