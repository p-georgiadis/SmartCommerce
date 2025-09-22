@description('Service Bus namespace name')
param serviceBusName string

@description('Location for Service Bus resources')
param location string = resourceGroup().location

@description('Tags to apply to resources')
param tags object = {}

@description('Environment name')
param environment string

@description('Service Bus SKU')
param sku string = 'Standard'

@description('Enable zone redundancy')
param zoneRedundant bool = environment == 'prod'

@description('Minimum TLS version')
param minimumTlsVersion string = '1.2'

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: serviceBusName
  location: location
  tags: tags
  sku: {
    name: sku
    tier: sku
    capacity: sku == 'Premium' ? 1 : null
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    zoneRedundant: zoneRedundant
    minimumTlsVersion: minimumTlsVersion
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: false
    premiumMessagingPartitions: sku == 'Premium' ? 1 : null
  }
}

// Queues
resource orderEventsQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'order-events'
  properties: {
    maxSizeInMegabytes: sku == 'Premium' ? 81920 : 1024
    defaultMessageTimeToLive: 'P14D' // 14 days
    deadLetteringOnMessageExpiration: true
    duplicateDetectionHistoryTimeWindow: 'PT10M'
    enableBatchedOperations: true
    enablePartitioning: sku != 'Premium'
    maxDeliveryCount: 5
    requiresDuplicateDetection: false
    requiresSession: false
    autoDeleteOnIdle: 'P10675199DT2H48M5.4775807S' // Max value
    lockDuration: 'PT1M'
  }
}

resource productEventsQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'product-events'
  properties: {
    maxSizeInMegabytes: sku == 'Premium' ? 81920 : 1024
    defaultMessageTimeToLive: 'P14D'
    deadLetteringOnMessageExpiration: true
    duplicateDetectionHistoryTimeWindow: 'PT10M'
    enableBatchedOperations: true
    enablePartitioning: sku != 'Premium'
    maxDeliveryCount: 5
    requiresDuplicateDetection: false
    requiresSession: false
    autoDeleteOnIdle: 'P10675199DT2H48M5.4775807S'
    lockDuration: 'PT1M'
  }
}

resource paymentEventsQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'payment-events'
  properties: {
    maxSizeInMegabytes: sku == 'Premium' ? 81920 : 1024
    defaultMessageTimeToLive: 'P14D'
    deadLetteringOnMessageExpiration: true
    duplicateDetectionHistoryTimeWindow: 'PT10M'
    enableBatchedOperations: true
    enablePartitioning: sku != 'Premium'
    maxDeliveryCount: 5
    requiresDuplicateDetection: false
    requiresSession: false
    autoDeleteOnIdle: 'P10675199DT2H48M5.4775807S'
    lockDuration: 'PT1M'
  }
}

resource userEventsQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'user-events'
  properties: {
    maxSizeInMegabytes: sku == 'Premium' ? 81920 : 1024
    defaultMessageTimeToLive: 'P14D'
    deadLetteringOnMessageExpiration: true
    duplicateDetectionHistoryTimeWindow: 'PT10M'
    enableBatchedOperations: true
    enablePartitioning: sku != 'Premium'
    maxDeliveryCount: 5
    requiresDuplicateDetection: false
    requiresSession: false
    autoDeleteOnIdle: 'P10675199DT2H48M5.4775807S'
    lockDuration: 'PT1M'
  }
}

resource notificationEventsQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'notification-events'
  properties: {
    maxSizeInMegabytes: sku == 'Premium' ? 81920 : 1024
    defaultMessageTimeToLive: 'P14D'
    deadLetteringOnMessageExpiration: true
    duplicateDetectionHistoryTimeWindow: 'PT10M'
    enableBatchedOperations: true
    enablePartitioning: sku != 'Premium'
    maxDeliveryCount: 5
    requiresDuplicateDetection: false
    requiresSession: false
    autoDeleteOnIdle: 'P10675199DT2H48M5.4775807S'
    lockDuration: 'PT1M'
  }
}

// Topics for pub-sub scenarios
resource integrationEventsTopic 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'integration-events'
  properties: {
    maxSizeInMegabytes: sku == 'Premium' ? 81920 : 1024
    defaultMessageTimeToLive: 'P14D'
    duplicateDetectionHistoryTimeWindow: 'PT10M'
    enableBatchedOperations: true
    enablePartitioning: sku != 'Premium'
    requiresDuplicateDetection: false
    autoDeleteOnIdle: 'P10675199DT2H48M5.4775807S'
  }
}

// Subscriptions for the integration events topic
resource recommendationSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: integrationEventsTopic
  name: 'recommendation-service'
  properties: {
    defaultMessageTimeToLive: 'P14D'
    deadLetteringOnMessageExpiration: true
    deadLetteringOnFilterEvaluationExceptions: false
    enableBatchedOperations: true
    maxDeliveryCount: 5
    requiresSession: false
    autoDeleteOnIdle: 'P10675199DT2H48M5.4775807S'
    lockDuration: 'PT1M'
  }
}

resource analyticsSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: integrationEventsTopic
  name: 'analytics-service'
  properties: {
    defaultMessageTimeToLive: 'P14D'
    deadLetteringOnMessageExpiration: true
    deadLetteringOnFilterEvaluationExceptions: false
    enableBatchedOperations: true
    maxDeliveryCount: 5
    requiresSession: false
    autoDeleteOnIdle: 'P10675199DT2H48M5.4775807S'
    lockDuration: 'PT1M'
  }
}

// Authorization rules
resource sendRule 'Microsoft.ServiceBus/namespaces/authorizationRules@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'SendOnlyAccessKey'
  properties: {
    rights: [
      'Send'
    ]
  }
}

resource listenRule 'Microsoft.ServiceBus/namespaces/authorizationRules@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'ListenOnlyAccessKey'
  properties: {
    rights: [
      'Listen'
    ]
  }
}

resource manageRule 'Microsoft.ServiceBus/namespaces/authorizationRules@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'RootManageSharedAccessKey'
  properties: {
    rights: [
      'Send'
      'Listen'
      'Manage'
    ]
  }
}

// Network rules (if Premium)
resource networkRuleSet 'Microsoft.ServiceBus/namespaces/networkRuleSets@2022-10-01-preview' = if (sku == 'Premium') {
  parent: serviceBusNamespace
  name: 'default'
  properties: {
    defaultAction: 'Allow'
    virtualNetworkRules: []
    ipRules: []
    trustedServiceAccessEnabled: true
    publicNetworkAccess: 'Enabled'
  }
}

// Diagnostic settings
resource diagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${serviceBusName}-diagnostics'
  scope: serviceBusNamespace
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'OperationalLogs'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: environment == 'prod' ? 90 : 30
        }
      }
      {
        category: 'VNetAndIPFilteringLogs'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: environment == 'prod' ? 90 : 30
        }
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: environment == 'prod' ? 90 : 30
        }
      }
    ]
  }
}

@description('Log Analytics workspace resource ID')
param logAnalyticsWorkspaceId string = ''

// Outputs
output serviceBusId string = serviceBusNamespace.id
output serviceBusName string = serviceBusNamespace.name
output serviceBusNamespace string = serviceBusNamespace.name
output serviceBusEndpoint string = serviceBusNamespace.properties.serviceBusEndpoint
output connectionString string = manageRule.listKeys().primaryConnectionString
output sendOnlyConnectionString string = sendRule.listKeys().primaryConnectionString
output listenOnlyConnectionString string = listenRule.listKeys().primaryConnectionString
output principalId string = serviceBusNamespace.identity.principalId