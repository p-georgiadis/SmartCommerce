@description('Base name for all resources')
param baseName string

@description('Environment name')
@allowed(['dev', 'staging', 'prod'])
param environment string

@description('Location for all resources')
param location string = resourceGroup().location

@description('Administrator login for SQL Database')
@secure()
param sqlAdminLogin string

@description('Administrator password for SQL Database')
@secure()
param sqlAdminPassword string

@description('Tags to apply to all resources')
param tags object = {
  Environment: environment
  Project: 'SmartCommerce'
  ManagedBy: 'Bicep'
}

// Variables
var naming = {
  appServicePlan: '${baseName}-plan-${environment}'
  keyVault: '${baseName}-kv-${environment}'
  appInsights: '${baseName}-ai-${environment}'
  logAnalytics: '${baseName}-logs-${environment}'
  serviceBus: '${baseName}-sb-${environment}'
  containerAppsEnv: '${baseName}-cae-${environment}'
  sqlServer: '${baseName}-sql-${environment}'
  redisCache: '${baseName}-redis-${environment}'
  containerRegistry: '${baseName}acr${environment}'
  frontDoor: '${baseName}-fd-${environment}'
}

var environmentConfig = {
  dev: {
    appServicePlanSku: 'B1'
    appServicePlanTier: 'Basic'
    appServicePlanCapacity: 1
    sqlDatabaseSku: 'Basic'
    sqlDatabaseTier: 'Basic'
    serviceBusSku: 'Standard'
    redisSku: 'Basic'
    containerAppsCpu: json('0.5')
    containerAppsMemory: '1Gi'
    containerAppsMinReplicas: 0
    containerAppsMaxReplicas: 3
  }
  staging: {
    appServicePlanSku: 'P1v3'
    appServicePlanTier: 'PremiumV3'
    appServicePlanCapacity: 2
    sqlDatabaseSku: 'S2'
    sqlDatabaseTier: 'Standard'
    serviceBusSku: 'Standard'
    redisSku: 'Standard'
    containerAppsCpu: json('1.0')
    containerAppsMemory: '2Gi'
    containerAppsMinReplicas: 1
    containerAppsMaxReplicas: 5
  }
  prod: {
    appServicePlanSku: 'P2v3'
    appServicePlanTier: 'PremiumV3'
    appServicePlanCapacity: 3
    sqlDatabaseSku: 'S4'
    sqlDatabaseTier: 'Standard'
    serviceBusSku: 'Premium'
    redisSku: 'Premium'
    containerAppsCpu: json('2.0')
    containerAppsMemory: '4Gi'
    containerAppsMinReplicas: 2
    containerAppsMaxReplicas: 10
  }
}

var config = environmentConfig[environment]

// Log Analytics Workspace
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: naming.logAnalytics
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: environment == 'prod' ? 90 : 30
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

// Application Insights
resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: naming.appInsights
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// Key Vault
module keyVault 'modules/keyvault.bicep' = {
  name: 'keyVault'
  params: {
    keyVaultName: naming.keyVault
    location: location
    tags: tags
    environment: environment
  }
}

// Virtual Network
module network 'modules/network.bicep' = {
  name: 'network'
  params: {
    vnetName: '${baseName}-vnet-${environment}'
    location: location
    tags: tags
    environment: environment
  }
}

// Container Registry
resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: naming.containerRegistry
  location: location
  tags: tags
  sku: {
    name: environment == 'prod' ? 'Premium' : 'Standard'
  }
  properties: {
    adminUserEnabled: true
    publicNetworkAccess: 'Enabled'
    zoneRedundancy: environment == 'prod' ? 'Enabled' : 'Disabled'
  }
  identity: {
    type: 'SystemAssigned'
  }
}

// App Service Plan for .NET services
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: naming.appServicePlan
  location: location
  tags: tags
  sku: {
    name: config.appServicePlanSku
    tier: config.appServicePlanTier
    capacity: config.appServicePlanCapacity
  }
  kind: 'linux'
  properties: {
    reserved: true
    zoneRedundant: environment == 'prod'
  }
}

// Container Apps Environment for Python services
resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: naming.containerAppsEnv
  location: location
  tags: tags
  properties: {
    daprAIInstrumentationKey: applicationInsights.properties.InstrumentationKey
    vnetConfiguration: {
      infrastructureSubnetId: network.outputs.containerAppsSubnetId
      internal: false
    }
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// SQL Database
module sqlDatabase 'modules/sql.bicep' = {
  name: 'sqlDatabase'
  params: {
    sqlServerName: naming.sqlServer
    databaseName: '${baseName}-db'
    location: location
    tags: tags
    environment: environment
    adminLogin: sqlAdminLogin
    adminPassword: sqlAdminPassword
    sku: config.sqlDatabaseSku
    tier: config.sqlDatabaseTier
    subnetId: network.outputs.databaseSubnetId
  }
}

// Redis Cache
resource redisCache 'Microsoft.Cache/redis@2023-08-01' = {
  name: naming.redisCache
  location: location
  tags: tags
  properties: {
    sku: {
      name: config.redisSku
      family: config.redisSku == 'Premium' ? 'P' : 'C'
      capacity: config.redisSku == 'Premium' ? 1 : 0
    }
    enableNonSslPort: false
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    redisConfiguration: {
      'maxmemory-policy': 'allkeys-lru'
    }
  }
}

// Service Bus
module serviceBus 'modules/servicebus.bicep' = {
  name: 'serviceBus'
  params: {
    serviceBusName: naming.serviceBus
    location: location
    tags: tags
    environment: environment
    sku: config.serviceBusSku
  }
}

// .NET App Services
module orderService 'modules/app-service.bicep' = {
  name: 'orderService'
  params: {
    appName: '${baseName}-order-${environment}'
    location: location
    tags: tags
    appServicePlanId: appServicePlan.id
    keyVaultName: keyVault.outputs.keyVaultName
    appInsightsConnectionString: applicationInsights.properties.ConnectionString
    environment: environment
    subnetId: network.outputs.appServiceSubnetId
    containerRegistryName: containerRegistry.name
  }
}

module catalogService 'modules/app-service.bicep' = {
  name: 'catalogService'
  params: {
    appName: '${baseName}-catalog-${environment}'
    location: location
    tags: tags
    appServicePlanId: appServicePlan.id
    keyVaultName: keyVault.outputs.keyVaultName
    appInsightsConnectionString: applicationInsights.properties.ConnectionString
    environment: environment
    subnetId: network.outputs.appServiceSubnetId
    containerRegistryName: containerRegistry.name
  }
}

module paymentService 'modules/app-service.bicep' = {
  name: 'paymentService'
  params: {
    appName: '${baseName}-payment-${environment}'
    location: location
    tags: tags
    appServicePlanId: appServicePlan.id
    keyVaultName: keyVault.outputs.keyVaultName
    appInsightsConnectionString: applicationInsights.properties.ConnectionString
    environment: environment
    subnetId: network.outputs.appServiceSubnetId
    containerRegistryName: containerRegistry.name
  }
}

module userService 'modules/app-service.bicep' = {
  name: 'userService'
  params: {
    appName: '${baseName}-user-${environment}'
    location: location
    tags: tags
    appServicePlanId: appServicePlan.id
    keyVaultName: keyVault.outputs.keyVaultName
    appInsightsConnectionString: applicationInsights.properties.ConnectionString
    environment: environment
    subnetId: network.outputs.appServiceSubnetId
    containerRegistryName: containerRegistry.name
  }
}

module notificationService 'modules/app-service.bicep' = {
  name: 'notificationService'
  params: {
    appName: '${baseName}-notification-${environment}'
    location: location
    tags: tags
    appServicePlanId: appServicePlan.id
    keyVaultName: keyVault.outputs.keyVaultName
    appInsightsConnectionString: applicationInsights.properties.ConnectionString
    environment: environment
    subnetId: network.outputs.appServiceSubnetId
    containerRegistryName: containerRegistry.name
  }
}

// Python Container Apps
module recommendationService 'modules/container-app.bicep' = {
  name: 'recommendationService'
  params: {
    containerAppName: '${baseName}-recommendation-${environment}'
    location: location
    tags: tags
    containerAppsEnvironmentId: containerAppsEnvironment.id
    keyVaultName: keyVault.outputs.keyVaultName
    appInsightsConnectionString: applicationInsights.properties.ConnectionString
    redisHostName: redisCache.properties.hostName
    environment: environment
    cpu: config.containerAppsCpu
    memory: config.containerAppsMemory
    minReplicas: config.containerAppsMinReplicas
    maxReplicas: config.containerAppsMaxReplicas
    containerRegistryName: containerRegistry.name
    imageName: 'recommendation-service'
  }
}

module priceOptimizationService 'modules/container-app.bicep' = {
  name: 'priceOptimizationService'
  params: {
    containerAppName: '${baseName}-price-${environment}'
    location: location
    tags: tags
    containerAppsEnvironmentId: containerAppsEnvironment.id
    keyVaultName: keyVault.outputs.keyVaultName
    appInsightsConnectionString: applicationInsights.properties.ConnectionString
    redisHostName: redisCache.properties.hostName
    environment: environment
    cpu: config.containerAppsCpu
    memory: config.containerAppsMemory
    minReplicas: config.containerAppsMinReplicas
    maxReplicas: config.containerAppsMaxReplicas
    containerRegistryName: containerRegistry.name
    imageName: 'price-optimization-service'
  }
}

module fraudDetectionService 'modules/container-app.bicep' = {
  name: 'fraudDetectionService'
  params: {
    containerAppName: '${baseName}-fraud-${environment}'
    location: location
    tags: tags
    containerAppsEnvironmentId: containerAppsEnvironment.id
    keyVaultName: keyVault.outputs.keyVaultName
    appInsightsConnectionString: applicationInsights.properties.ConnectionString
    redisHostName: redisCache.properties.hostName
    environment: environment
    cpu: config.containerAppsCpu
    memory: config.containerAppsMemory
    minReplicas: config.containerAppsMinReplicas
    maxReplicas: config.containerAppsMaxReplicas
    containerRegistryName: containerRegistry.name
    imageName: 'fraud-detection-service'
  }
}

// Key Vault Secrets
resource sqlConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault.outputs.keyVaultResource
  name: 'SqlConnectionString'
  properties: {
    value: sqlDatabase.outputs.connectionString
  }
}

resource serviceBusConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault.outputs.keyVaultResource
  name: 'ServiceBusConnectionString'
  properties: {
    value: serviceBus.outputs.connectionString
  }
}

resource redisConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault.outputs.keyVaultResource
  name: 'RedisConnectionString'
  properties: {
    value: '${redisCache.properties.hostName}:6380,password=${redisCache.listKeys().primaryKey},ssl=True,abortConnect=False'
  }
}

// Front Door (Optional for production)
module frontDoor 'modules/frontdoor.bicep' = if (environment == 'prod') {
  name: 'frontDoor'
  params: {
    frontDoorName: naming.frontDoor
    tags: tags
    orderServiceHostname: orderService.outputs.defaultHostname
    catalogServiceHostname: catalogService.outputs.defaultHostname
    paymentServiceHostname: paymentService.outputs.defaultHostname
    userServiceHostname: userService.outputs.defaultHostname
  }
}

// Outputs
output resourceGroupName string = resourceGroup().name
output location string = location
output environment string = environment

output appServicePlanId string = appServicePlan.id
output keyVaultUri string = keyVault.outputs.keyVaultUri
output keyVaultName string = keyVault.outputs.keyVaultName
output appInsightsConnectionString string = applicationInsights.properties.ConnectionString
output appInsightsInstrumentationKey string = applicationInsights.properties.InstrumentationKey

output containerRegistryLoginServer string = containerRegistry.properties.loginServer
output containerRegistryName string = containerRegistry.name

output sqlServerFqdn string = sqlDatabase.outputs.sqlServerFqdn
output sqlDatabaseName string = sqlDatabase.outputs.databaseName

output serviceBusNamespace string = serviceBus.outputs.serviceBusNamespace
output redisHostName string = redisCache.properties.hostName

output orderServiceUrl string = 'https://${orderService.outputs.defaultHostname}'
output catalogServiceUrl string = 'https://${catalogService.outputs.defaultHostname}'
output paymentServiceUrl string = 'https://${paymentService.outputs.defaultHostname}'
output userServiceUrl string = 'https://${userService.outputs.defaultHostname}'
output notificationServiceUrl string = 'https://${notificationService.outputs.defaultHostname}'

output recommendationServiceUrl string = recommendationService.outputs.applicationUrl
output priceOptimizationServiceUrl string = priceOptimizationService.outputs.applicationUrl
output fraudDetectionServiceUrl string = fraudDetectionService.outputs.applicationUrl

output frontDoorEndpoint string = environment == 'prod' ? frontDoor.outputs.frontDoorEndpoint : ''

output deploymentSummary object = {
  environment: environment
  location: location
  totalResources: 25
  dotnetServices: 5
  pythonServices: 3
  infrastructure: 17
  estimatedMonthlyCost: environment == 'prod' ? '$500-800' : environment == 'staging' ? '$300-500' : '$100-200'
}