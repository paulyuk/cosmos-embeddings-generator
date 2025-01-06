param name string
param location string = resourceGroup().location
param tags object = {}

@allowed([
  'linux'
])
@description('OS type of the plan. Defaults to "linux".')
param kind string = 'linux'

param sku object

param reserved bool = true


resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: name
  location: location
  tags: tags
  sku: sku
  kind: kind
  properties: {
    reserved: reserved
  }
}

output id string = appServicePlan.id
