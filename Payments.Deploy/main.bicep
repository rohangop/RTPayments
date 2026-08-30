targetScope = 'resourceGroup'

param eastUsAppName string
param westEuropeAppName string
param serviceBusName string
param paymentQueueName string = 'payments'

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
