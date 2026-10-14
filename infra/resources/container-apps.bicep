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

@minValue(0)
@maxValue(30)
@description('Minimum replicas for the web app')
param webMinReplicas int = 0

@secure()
@description('PostgreSQL connection string')
param postgresqlConnectionString string

@secure()
@description('Redis connection string for the SignalR backplane')
param redisConnectionString string

@secure()
@description('Key that signs the Active User identity issued by the API')
param authSigningKey string

@description('Azure Container Registry login server')
param containerRegistryLoginServer string

@secure()
@description('Azure Container Registry admin username')
param containerRegistryUsername string

@secure()
@description('Azure Container Registry admin password')
param containerRegistryPassword string

// === VARIABLES ===

// Liveness never depends on the database (/alive); readiness does (/health). The startup
// probe gives each replica 2 minutes to start.
var probes = [
  {
    type: 'Startup'
    httpGet: {
      path: '/alive'
      port: 8080
    }
    periodSeconds: 5
    failureThreshold: 24
  }
  {
    type: 'Liveness'
    httpGet: {
      path: '/alive'
      port: 8080
    }
    periodSeconds: 10
    failureThreshold: 3
  }
  {
    type: 'Readiness'
    httpGet: {
      path: '/health'
      port: 8080
    }
    periodSeconds: 10
    failureThreshold: 3
  }
]

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
      activeRevisionsMode: 'Single'
      // Internal: only apps in this environment, such as the web app, can reach the API.
      // No environment-level HTTP route targets it (FR-038).
      ingress: {
        external: false
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
          name: 'auth-signing-key'
          value: authSigningKey
        }
        {
          name: 'redis-connection-string'
          value: redisConnectionString
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
          probes: probes
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
              name: 'Auth__SigningKey'
              secretRef: 'auth-signing-key'
            }
            {
              name: 'Auth__Issuer'
              value: 'taskify'
            }
            {
              name: 'Auth__Audience'
              value: 'taskify-api'
            }
            {
              name: 'ConnectionStrings__redis'
              secretRef: 'redis-connection-string'
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
      // Session affinity only works in single revision mode.
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
        // A Blazor Server circuit lives in one replica; keep each browser on it (FR-033).
        stickySessions: {
          affinity: 'sticky'
        }
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
          probes: probes
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
        minReplicas: webMinReplicas
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
