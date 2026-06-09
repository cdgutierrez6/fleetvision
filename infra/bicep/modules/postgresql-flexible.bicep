param location string
param tags object
param environment string
param adminPasswordSecretId string

var serverName = 'fv-postgres-${environment}-${uniqueString(resourceGroup().id)}'

// Burstable B1ms: 1 vCore, 2 GB RAM — sufficient for dev/staging
// Upgrade to General Purpose for production workloads
resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2023-12-01-preview' = {
  name: serverName
  location: location
  tags: tags
  sku: {
    name: environment == 'prod' ? 'Standard_D2ds_v4' : 'Standard_B1ms'
    tier: environment == 'prod' ? 'GeneralPurpose' : 'Burstable'
  }
  properties: {
    administratorLogin: 'fv_admin'
    administratorLoginPassword: adminPasswordSecretId
    version: '16'
    storage: {
      storageSizeGB: environment == 'prod' ? 128 : 32
      autoGrow: 'Enabled'
    }
    backup: {
      backupRetentionDays: environment == 'prod' ? 14 : 7
      geoRedundantBackup: environment == 'prod' ? 'Enabled' : 'Disabled'
    }
    highAvailability: {
      mode: environment == 'prod' ? 'ZoneRedundant' : 'Disabled'
    }
  }
}

// Enable extensions required by FleetVision
resource postgresConfig 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2023-12-01-preview' = {
  parent: postgres
  name: 'azure.extensions'
  properties: {
    // TimescaleDB is available in Azure DB for PostgreSQL Flexible as a preview extension
    // PostGIS is GA
    value: 'TIMESCALEDB,POSTGIS,PG_TRGM,UUID-OSSP'
    source: 'user-override'
  }
}

// Allow Azure services (Container Apps) to connect
resource firewallRule 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2023-12-01-preview' = {
  parent: postgres
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output serverFqdn string = postgres.properties.fullyQualifiedDomainName
output serverName string = postgres.name
