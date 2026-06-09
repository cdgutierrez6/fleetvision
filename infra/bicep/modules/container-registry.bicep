param location string
param sku string = 'Basic'
param tags object

var acrName = 'fleetvision${uniqueString(resourceGroup().id)}'

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  tags: tags
  sku: {
    name: sku
  }
  properties: {
    adminUserEnabled: false
    anonymousPullEnabled: false
  }
}

output loginServer string = acr.properties.loginServer
output acrName string = acr.name
