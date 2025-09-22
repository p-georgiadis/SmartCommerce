@description('SQL Server name')
param sqlServerName string

@description('Database name')
param databaseName string

@description('Location for SQL resources')
param location string = resourceGroup().location

@description('Tags to apply to resources')
param tags object = {}

@description('Environment name')
param environment string

@description('Administrator login username')
@secure()
param adminLogin string

@description('Administrator login password')
@secure()
param adminPassword string

@description('Database SKU name')
param sku string = 'S2'

@description('Database tier')
param tier string = 'Standard'

@description('Subnet ID for private endpoint')
param subnetId string = ''

@description('Enable Azure AD authentication')
param enableAzureADAuth bool = true

@description('Azure AD admin object ID')
param azureADAdminObjectId string = ''

@description('Azure AD admin principal name')
param azureADAdminPrincipalName string = ''

@description('Maximum database size in bytes')
param maxSizeBytes int = environment == 'prod' ? 268435456000 : 2147483648 // 250GB for prod, 2GB for others

resource sqlServer 'Microsoft.Sql/servers@2022-05-01-preview' = {
  name: sqlServerName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    administratorLogin: adminLogin
    administratorLoginPassword: adminPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled' // Will be restricted by firewall rules
    restrictOutboundNetworkAccess: 'Disabled'
    administrators: enableAzureADAuth && !empty(azureADAdminObjectId) ? {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: false
      login: azureADAdminPrincipalName
      principalType: 'User'
      sid: azureADAdminObjectId
      tenantId: subscription().tenantId
    } : null
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2022-05-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  tags: tags
  sku: {
    name: sku
    tier: tier
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: maxSizeBytes
    catalogCollation: 'SQL_Latin1_General_CP1_CI_AS'
    zoneRedundant: environment == 'prod'
    readScale: environment == 'prod' ? 'Enabled' : 'Disabled'
    requestedBackupStorageRedundancy: environment == 'prod' ? 'Geo' : 'Local'
    isLedgerOn: false
    maintenanceConfigurationId: environment == 'prod'
      ? '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Maintenance/publicMaintenanceConfigurations/SQL_Default'
      : '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Maintenance/publicMaintenanceConfigurations/SQL_WestEurope_DB_1'
  }
}

// Firewall rules
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2022-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Virtual Network Rules for subnet access
resource vnetRule 'Microsoft.Sql/servers/virtualNetworkRules@2022-05-01-preview' = if (!empty(subnetId)) {
  parent: sqlServer
  name: 'vnet-rule'
  properties: {
    virtualNetworkSubnetId: subnetId
    ignoreMissingVnetServiceEndpoint: false
  }
}

// Auditing configuration
resource auditing 'Microsoft.Sql/servers/auditingSettings@2022-05-01-preview' = {
  parent: sqlServer
  name: 'default'
  properties: {
    state: 'Enabled'
    storageEndpoint: storageAccount.properties.primaryEndpoints.blob
    storageAccountAccessKey: storageAccount.listKeys().keys[0].value
    retentionDays: environment == 'prod' ? 90 : 30
    auditActionsAndGroups: [
      'SUCCESSFUL_DATABASE_AUTHENTICATION_GROUP'
      'FAILED_DATABASE_AUTHENTICATION_GROUP'
      'BATCH_COMPLETED_GROUP'
    ]
    isStorageSecondaryKeyInUse: false
    isAzureMonitorTargetEnabled: true
  }
}

// Security alert policies
resource securityAlertPolicies 'Microsoft.Sql/servers/securityAlertPolicies@2022-05-01-preview' = {
  parent: sqlServer
  name: 'default'
  properties: {
    state: 'Enabled'
    disabledAlerts: []
    emailAddresses: environment == 'prod' ? ['alerts@company.com'] : []
    emailAccountAdmins: environment == 'prod'
    retentionDays: environment == 'prod' ? 30 : 7
  }
}

// Vulnerability assessments
resource vulnerabilityAssessments 'Microsoft.Sql/servers/vulnerabilityAssessments@2022-05-01-preview' = {
  parent: sqlServer
  name: 'default'
  dependsOn: [auditing]
  properties: {
    storageContainerPath: '${storageAccount.properties.primaryEndpoints.blob}vulnerability-assessment'
    storageAccountAccessKey: storageAccount.listKeys().keys[0].value
    recurringScans: {
      isEnabled: true
      emailSubscriptionAdmins: environment == 'prod'
      emails: environment == 'prod' ? ['security@company.com'] : []
    }
  }
}

// Transparent Data Encryption
resource transparentDataEncryption 'Microsoft.Sql/servers/databases/transparentDataEncryption@2022-05-01-preview' = {
  parent: sqlDatabase
  name: 'current'
  properties: {
    state: 'Enabled'
  }
}

// Storage account for auditing and vulnerability assessments
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: 'sql${uniqueString(resourceGroup().id, sqlServerName)}'
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

// Blob container for vulnerability assessments
resource vulnerabilityAssessmentContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  name: '${storageAccount.name}/default/vulnerability-assessment'
  properties: {
    publicAccess: 'None'
  }
}

// Diagnostic settings
resource diagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${sqlServerName}-diagnostics'
  scope: sqlDatabase
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'SQLInsights'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: environment == 'prod' ? 90 : 30
        }
      }
      {
        category: 'AutomaticTuning'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: environment == 'prod' ? 90 : 30
        }
      }
      {
        category: 'QueryStoreRuntimeStatistics'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: environment == 'prod' ? 90 : 30
        }
      }
      {
        category: 'QueryStoreWaitStatistics'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: environment == 'prod' ? 90 : 30
        }
      }
      {
        category: 'Errors'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: environment == 'prod' ? 90 : 30
        }
      }
      {
        category: 'DatabaseWaitStatistics'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: environment == 'prod' ? 90 : 30
        }
      }
      {
        category: 'Timeouts'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: environment == 'prod' ? 90 : 30
        }
      }
      {
        category: 'Blocks'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: environment == 'prod' ? 90 : 30
        }
      }
      {
        category: 'Deadlocks'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: environment == 'prod' ? 90 : 30
        }
      }
    ]
    metrics: [
      {
        category: 'Basic'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: environment == 'prod' ? 90 : 30
        }
      }
      {
        category: 'InstanceAndAppAdvanced'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: environment == 'prod' ? 90 : 30
        }
      }
      {
        category: 'WorkloadManagement'
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
output sqlServerId string = sqlServer.id
output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output databaseId string = sqlDatabase.id
output databaseName string = sqlDatabase.name
output connectionString string = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabase.name};Persist Security Info=False;User ID=${adminLogin};Password=${adminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
output sqlServerPrincipalId string = sqlServer.identity.principalId