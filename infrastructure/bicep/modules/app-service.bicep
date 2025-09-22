@description('Name of the App Service')
param appName string

@description('Location for the App Service')
param location string = resourceGroup().location

@description('Tags to apply to resources')
param tags object = {}

@description('App Service Plan resource ID')
param appServicePlanId string

@description('Key Vault name for configuration')
param keyVaultName string

@description('Application Insights connection string')
param appInsightsConnectionString string

@description('Environment name')
param environment string

@description('Subnet ID for VNet integration')
param subnetId string = ''

@description('Container registry name')
param containerRegistryName string

@description('Runtime stack')
param runtimeStack string = 'DOTNETCORE|8.0'

@description('Always On setting')
param alwaysOn bool = environment != 'dev'

@description('Health check path')
param healthCheckPath string = '/health'

@description('Enable detailed error messages')
param detailedErrorLoggingEnabled bool = environment == 'dev'

@description('Enable request tracing')
param requestTracingEnabled bool = environment == 'dev'

@description('Enable HTTP logging')
param httpLoggingEnabled bool = true

resource appService 'Microsoft.Web/sites@2023-01-01' = {
  name: appName
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlanId
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      linuxFxVersion: runtimeStack
      alwaysOn: alwaysOn
      healthCheckPath: healthCheckPath
      detailedErrorLoggingEnabled: detailedErrorLoggingEnabled
      requestTracingEnabled: requestTracingEnabled
      httpLoggingEnabled: httpLoggingEnabled
      logsDirectorySizeLimit: 40
      use32BitWorkerProcess: false
      webSocketsEnabled: false
      managedPipelineMode: 'Integrated'
      loadBalancing: 'LeastRequests'
      experiments: {
        rampUpRules: []
      }
      autoHealEnabled: true
      autoHealRules: {
        triggers: {
          privateBytesInKB: 1048576 // 1GB
          statusCodes: [
            {
              status: 500
              subStatus: 0
              win32Status: 0
              count: 10
              timeInterval: '00:05:00'
            }
          ]
        }
        actions: {
          actionType: 'Recycle'
          minProcessExecutionTime: '00:01:00'
        }
      }
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
        {
          name: 'XDT_MicrosoftApplicationInsights_Mode'
          value: 'Recommended'
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environment == 'prod' ? 'Production' : (environment == 'staging' ? 'Staging' : 'Development')
        }
        {
          name: 'KeyVault__Uri'
          value: 'https://${keyVaultName}.vault.azure.net/'
        }
        {
          name: 'ConnectionStrings__ServiceBus'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=ServiceBusConnectionString)'
        }
        {
          name: 'ConnectionStrings__OrderDb'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=SqlConnectionString)'
        }
        {
          name: 'ConnectionStrings__CatalogDb'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=SqlConnectionString)'
        }
        {
          name: 'ConnectionStrings__PaymentDb'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=SqlConnectionString)'
        }
        {
          name: 'ConnectionStrings__UserDb'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=SqlConnectionString)'
        }
        {
          name: 'ConnectionStrings__Redis'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=RedisConnectionString)'
        }
        {
          name: 'WEBSITE_ENABLE_SYNC_UPDATE_SITE'
          value: 'true'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'SCM_DO_BUILD_DURING_DEPLOYMENT'
          value: 'false'
        }
        {
          name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
          value: 'false'
        }
        {
          name: 'DOCKER_REGISTRY_SERVER_URL'
          value: 'https://${containerRegistryName}.azurecr.io'
        }
        {
          name: 'DOCKER_REGISTRY_SERVER_USERNAME'
          value: containerRegistryName
        }
        {
          name: 'DOCKER_REGISTRY_SERVER_PASSWORD'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=ContainerRegistryPassword)'
        }
      ]
      connectionStrings: []
    }
    vnetRouteAllEnabled: !empty(subnetId)
    vnetImagePullEnabled: !empty(subnetId)
    vnetContentShareEnabled: false
  }
}

// VNet integration
resource vnetIntegration 'Microsoft.Web/sites/networkConfig@2023-01-01' = if (!empty(subnetId)) {
  parent: appService
  name: 'virtualNetwork'
  properties: {
    subnetResourceId: subnetId
    swiftSupported: true
  }
}

// Staging slot for blue-green deployments
resource stagingSlot 'Microsoft.Web/sites/slots@2023-01-01' = if (environment != 'dev') {
  parent: appService
  name: 'staging'
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlanId
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      linuxFxVersion: runtimeStack
      alwaysOn: alwaysOn
      healthCheckPath: healthCheckPath
      appSettings: appService.properties.siteConfig.appSettings
    }
  }
}

// Auto-swap configuration for production
resource autoSwap 'Microsoft.Web/sites/slots/config@2023-01-01' = if (environment == 'prod') {
  parent: stagingSlot
  name: 'web'
  properties: {
    autoSwapSlotName: 'production'
  }
}

// Diagnostic settings
resource diagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${appName}-diagnostics'
  scope: appService
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'AppServiceHTTPLogs'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: environment == 'prod' ? 30 : 7
        }
      }
      {
        category: 'AppServiceConsoleLogs'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: environment == 'prod' ? 30 : 7
        }
      }
      {
        category: 'AppServiceAppLogs'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: environment == 'prod' ? 30 : 7
        }
      }
      {
        category: 'AppServicePlatformLogs'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: environment == 'prod' ? 30 : 7
        }
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: environment == 'prod' ? 30 : 7
        }
      }
    ]
  }
}

@description('Log Analytics workspace resource ID')
param logAnalyticsWorkspaceId string = ''

// Outputs
output appServiceId string = appService.id
output appServiceName string = appService.name
output defaultHostname string = appService.properties.defaultHostName
output principalId string = appService.identity.principalId
output stagingSlotName string = environment != 'dev' ? stagingSlot.name : ''
output stagingSlotPrincipalId string = environment != 'dev' ? stagingSlot.identity.principalId : ''