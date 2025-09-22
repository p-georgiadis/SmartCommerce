@description('Container App name')
param containerAppName string

@description('Location for the Container App')
param location string = resourceGroup().location

@description('Tags to apply to resources')
param tags object = {}

@description('Container Apps Environment resource ID')
param containerAppsEnvironmentId string

@description('Key Vault name for configuration')
param keyVaultName string

@description('Application Insights connection string')
param appInsightsConnectionString string

@description('Redis hostname')
param redisHostName string

@description('Environment name')
param environment string

@description('CPU allocation')
param cpu object = json('0.5')

@description('Memory allocation')
param memory string = '1Gi'

@description('Minimum replicas')
param minReplicas int = 0

@description('Maximum replicas')
param maxReplicas int = 3

@description('Container registry name')
param containerRegistryName string

@description('Container image name')
param imageName string

@description('Target port for ingress')
param targetPort int = 8000

@description('Enable external ingress')
param externalIngress bool = false

@description('Enable Dapr')
param enableDapr bool = true

@description('Dapr app ID')
param daprAppId string = containerAppName

resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: containerAppName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironmentId
    configuration: {
      activeRevisionsMode: 'Multiple'
      maxInactiveRevisions: 5
      ingress: {
        external: externalIngress
        targetPort: targetPort
        transport: 'auto'
        allowInsecure: false
        traffic: [
          {
            weight: 100
            latestRevision: true
          }
        ]
      }
      dapr: enableDapr ? {
        enabled: true
        appId: daprAppId
        appPort: targetPort
        appProtocol: 'http'
        enableApiLogging: environment == 'dev'
        logLevel: environment == 'dev' ? 'debug' : 'info'
      } : null
      registries: [
        {
          server: '${containerRegistryName}.azurecr.io'
          identity: resourceId('Microsoft.ManagedIdentity/userAssignedIdentities', '${containerAppName}-identity')
        }
      ]
      secrets: [
        {
          name: 'registry-password'
          keyVaultUrl: 'https://${keyVaultName}.vault.azure.net/secrets/ContainerRegistryPassword'
          identity: resourceId('Microsoft.ManagedIdentity/userAssignedIdentities', '${containerAppName}-identity')
        }
        {
          name: 'redis-connection'
          keyVaultUrl: 'https://${keyVaultName}.vault.azure.net/secrets/RedisConnectionString'
          identity: resourceId('Microsoft.ManagedIdentity/userAssignedIdentities', '${containerAppName}-identity')
        }
        {
          name: 'servicebus-connection'
          keyVaultUrl: 'https://${keyVaultName}.vault.azure.net/secrets/ServiceBusConnectionString'
          identity: resourceId('Microsoft.ManagedIdentity/userAssignedIdentities', '${containerAppName}-identity')
        }
      ]
    }
    template: {
      revisionSuffix: 'v${utcNow()}'
      containers: [
        {
          image: '${containerRegistryName}.azurecr.io/${imageName}:latest'
          name: imageName
          resources: {
            cpu: cpu
            memory: memory
          }
          env: [
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: appInsightsConnectionString
            }
            {
              name: 'REDIS_HOST'
              value: redisHostName
            }
            {
              name: 'REDIS_CONNECTION_STRING'
              secretRef: 'redis-connection'
            }
            {
              name: 'SERVICE_BUS_CONNECTION_STRING'
              secretRef: 'servicebus-connection'
            }
            {
              name: 'ENVIRONMENT'
              value: environment
            }
            {
              name: 'LOG_LEVEL'
              value: environment == 'dev' ? 'DEBUG' : 'INFO'
            }
            {
              name: 'PYTHONUNBUFFERED'
              value: '1'
            }
            {
              name: 'PORT'
              value: string(targetPort)
            }
            {
              name: 'HOST'
              value: '0.0.0.0'
            }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: targetPort
                scheme: 'HTTP'
              }
              initialDelaySeconds: 30
              periodSeconds: 30
              timeoutSeconds: 10
              failureThreshold: 3
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: targetPort
                scheme: 'HTTP'
              }
              initialDelaySeconds: 10
              periodSeconds: 10
              timeoutSeconds: 5
              failureThreshold: 3
            }
          ]
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: [
          {
            name: 'http-scaling'
            http: {
              metadata: {
                concurrentRequests: '50'
              }
            }
          }
          {
            name: 'cpu-scaling'
            custom: {
              type: 'cpu'
              metadata: {
                type: 'Utilization'
                value: '70'
              }
            }
          }
          {
            name: 'memory-scaling'
            custom: {
              type: 'memory'
              metadata: {
                type: 'Utilization'
                value: '80'
              }
            }
          }
        ]
      }
    }
  }
}

// User-assigned identity for Key Vault access
resource userIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${containerAppName}-identity'
  location: location
  tags: tags
}

// Assign Container App the user identity
resource containerAppIdentityAssignment 'Microsoft.App/containerApps@2023-05-01' = {
  name: containerAppName
  location: location
  tags: tags
  dependsOn: [containerApp]
  identity: {
    type: 'SystemAssigned,UserAssigned'
    userAssignedIdentities: {
      '${userIdentity.id}': {}
    }
  }
  properties: containerApp.properties
}

@description('Current UTC time for revision suffix')
param utcNow string = utcNow('yyyyMMddHHmmss')

// Outputs
output containerAppId string = containerApp.id
output containerAppName string = containerApp.name
output applicationUrl string = containerApp.properties.configuration.ingress.fqdn != null ? 'https://${containerApp.properties.configuration.ingress.fqdn}' : ''
output principalId string = containerApp.identity.principalId
output userIdentityId string = userIdentity.id
output userIdentityPrincipalId string = userIdentity.properties.principalId