param location string
param tags object
param environment string

var kvName = 'fv-kv-${environment}-${uniqueString(resourceGroup().id)}'

resource kv 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: kvName
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    enablePurgeProtection: environment == 'prod' ? true : false
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

// Placeholder secret — actual value set via CI/CD pipeline
resource postgresPasswordSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: kv
  name: 'postgres-admin-password'
  properties: {
    value: 'PLACEHOLDER_SET_BY_PIPELINE'
    attributes: {
      enabled: true
    }
  }
}

output keyVaultId string = kv.id
output keyVaultName string = kv.name
output postgresPasswordSecretId string = postgresPasswordSecret.properties.secretUri
