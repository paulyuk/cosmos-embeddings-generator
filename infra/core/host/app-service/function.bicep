targetScope = 'subscription'
param appName string
param location string
param functionAppName string
param cosmosDbAccountName string
param cosmosDbDatabaseName string
param cosmosDbContainerName string
param functionAppResource resource

resource httpTrigger 'Microsoft.Web/sites/functions@2021-02-01' = {
  parent: functionAppResource
  name: 'HttpTrigger'
  properties: {
    script: {
      main: 'index'
      scriptFile: 'index.js'
    }
    bindings: [
      {
        type: 'httpTrigger'
        direction: 'in'
        name: 'req'
        authLevel: 'anonymous'
        methods: [
          'get'
          'post'
        ]
      }
      {
        type: 'http'
        direction: 'out'
        name: 'res'
      }
    ]
  }
}

resource cosmosDbTrigger 'Microsoft.Web/sites/functions@2021-02-01' = {
  parent: functionAppResource
  name: 'CosmosDbTrigger'
  properties: {
    script: {
      main: 'index'
      scriptFile: 'index.js'
    }
    bindings: [
      {
        type: 'cosmosDBTrigger'
        direction: 'in'
        name: 'documents'
        leaseCollectionName: 'leases'
        connectionStringSetting: 'CosmosDBConnectionString'
        databaseName: cosmosDbDatabaseName
        collectionName: cosmosDbContainerName
      }
    ]
  }
}
