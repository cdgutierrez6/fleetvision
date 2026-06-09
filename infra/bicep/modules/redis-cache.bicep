param location string
param tags object
param environment string

var cacheName = 'fv-redis-${environment}-${uniqueString(resourceGroup().id)}'

// Basic C0 (250 MB) — sufficient for odometer cache + session state
// Upgrade to Standard C1 for HA in production
resource redis 'Microsoft.Cache/redis@2024-03-01' = {
  name: cacheName
  location: location
  tags: tags
  properties: {
    sku: {
      name: environment == 'prod' ? 'Standard' : 'Basic'
      family: 'C'
      capacity: environment == 'prod' ? 1 : 0
    }
    enableNonSslPort: false
    minimumTlsVersion: '1.2'
    redisConfiguration: {
      maxmemoryPolicy: 'allkeys-lru'
    }
  }
}

output hostName string = redis.properties.hostName
output sslPort int = redis.properties.sslPort
output cacheId string = redis.id
