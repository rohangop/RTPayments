targetScope = 'resourceGroup'

param eastUsAppName string
param westEuropeAppName string
param eastUsFunctionAppName string
param westEuropeFunctionAppName string
param eastUsFunctionStorageName string
param westEuropeFunctionStorageName string
param serviceBusName string
param paymentQueueName string = 'payments'
param primarySqlServerName string
param secondarySqlServerName string
param sqlDatabaseName string = 'Payments'
param sqlAdministratorLogin string
@secure()
param sqlAdministratorPassword string
param sqlFailoverGroupName string = 'payments-failover-group'

resource eastUsPlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: '${eastUsAppName}-plan'
  location: 'East US'
  kind: 'linux'
  properties: {
    reserved: true
  }
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
}

resource westEuropePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: '${westEuropeAppName}-plan'
  location: 'West Europe'
  kind: 'linux'
  properties: {
    reserved: true
  }
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
}

resource eastUsApp 'Microsoft.Web/sites@2022-03-01' = {
  name: eastUsAppName
  location: 'East US'
  kind: 'app,linux'
  properties: {
    serverFarmId: eastUsPlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
    }
  }
}

resource westEuropeApp 'Microsoft.Web/sites@2022-03-01' = {
  name: westEuropeAppName
  location: 'West Europe'
  kind: 'app,linux'
  properties: {
    serverFarmId: westEuropePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
    }
  }
}

resource eastUsFunctionStorage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: eastUsFunctionStorageName
  location: 'East US'
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

resource westEuropeFunctionStorage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: westEuropeFunctionStorageName
  location: 'West Europe'
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

resource eastUsFunctionPlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: '${eastUsFunctionAppName}-plan'
  location: 'East US'
  kind: 'functionapp'
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
}

resource westEuropeFunctionPlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: '${westEuropeFunctionAppName}-plan'
  location: 'West Europe'
  kind: 'functionapp'
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
}

resource eastUsFunctionApp 'Microsoft.Web/sites@2022-03-01' = {
  name: eastUsFunctionAppName
  location: 'East US'
  kind: 'functionapp,linux'
  properties: {
    serverFarmId: eastUsFunctionPlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      appSettings: [
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'AzureWebJobsStorage__accountName'
          value: eastUsFunctionStorage.name
        }
      ]
    }
  }
}

resource westEuropeFunctionApp 'Microsoft.Web/sites@2022-03-01' = {
  name: westEuropeFunctionAppName
  location: 'West Europe'
  kind: 'functionapp,linux'
  properties: {
    serverFarmId: westEuropeFunctionPlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      appSettings: [
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'AzureWebJobsStorage__accountName'
          value: westEuropeFunctionStorage.name
        }
      ]
    }
  }
}

resource serviceBus 'Microsoft.ServiceBus/namespaces@2025-05-01-preview' = {
  name: serviceBusName
  location: 'East US'
  sku: {
    name: 'Premium'
    tier: 'Premium'
    capacity: 1
  }
  properties: {
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    geoDataReplication: {
      // Zero lag configures synchronous replication for RPO 0.
      maxReplicationLagDurationInSeconds: 0
      locations: [
        {
          locationName: 'East US'
          roleType: 'Primary'
        }
        {
          locationName: 'West Europe'
          roleType: 'Secondary'
        }
      ]
    }
  }
}

resource paymentQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBus
  name: paymentQueueName
  properties: {
    maxDeliveryCount: 10
    deadLetteringOnMessageExpiration: true
    requiresDuplicateDetection: true
    duplicateDetectionHistoryTimeWindow: 'PT10M'
  }
}

resource primarySqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: primarySqlServerName
  location: 'East US'
  properties: {
    administratorLogin: sqlAdministratorLogin
    administratorLoginPassword: sqlAdministratorPassword
    version: '12.0'
  }
}

resource secondarySqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: secondarySqlServerName
  location: 'West Europe'
  properties: {
    administratorLogin: sqlAdministratorLogin
    administratorLoginPassword: sqlAdministratorPassword
    version: '12.0'
  }
}

resource primarySqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: primarySqlServer
  name: sqlDatabaseName
  location: 'East US'
  sku: {
    name: 'S0'
    tier: 'Standard'
  }
}

resource secondarySqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: secondarySqlServer
  name: sqlDatabaseName
  location: 'West Europe'
  properties: {
    createMode: 'Secondary'
    sourceDatabaseId: primarySqlDatabase.id
  }
  sku: {
    name: 'S0'
    tier: 'Standard'
  }
}

resource sqlFailoverGroup 'Microsoft.Sql/servers/failoverGroups@2022-05-01-preview' = {
  parent: primarySqlServer
  name: sqlFailoverGroupName
  properties: {
    databases: [
      primarySqlDatabase.id
    ]
    partnerServers: [
      {
        id: secondarySqlServer.id
      }
    ]
    readWriteEndpoint: {
      failoverPolicy: 'Manual'
    }
    readOnlyEndpoint: {
      failoverPolicy: 'Disabled'
    }
  }
}
