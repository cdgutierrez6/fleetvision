targetScope = 'resourceGroup'

@description('Environment name (dev/staging/prod)')
param environment string

@description('Azure region')
param location string = resourceGroup().location

@description('Container Apps environment name')
param envName string = 'fleetvision-${environment}'

@allowed(['Free', 'Basic', 'Standard'])
param acrSku string = 'Basic'

var tags = {
  project: 'fleetvision'
  environment: environment
  managedBy: 'bicep'
}

// Container Registry
module acr 'modules/container-registry.bicep' = {
  name: 'acr'
  params: {
    location: location
    sku: acrSku
    tags: tags
  }
}

// Key Vault
module kv 'modules/key-vault.bicep' = {
  name: 'keyvault'
  params: {
    location: location
    tags: tags
    environment: environment
  }
}

// PostgreSQL Flexible Server (includes TimescaleDB extension)
module postgres 'modules/postgresql-flexible.bicep' = {
  name: 'postgres'
  params: {
    location: location
    tags: tags
    environment: environment
    adminPasswordSecretId: kv.outputs.postgresPasswordSecretId
  }
}

// Redis Cache
module redis 'modules/redis-cache.bicep' = {
  name: 'redis'
  params: {
    location: location
    tags: tags
    environment: environment
  }
}

// Container Apps Environment (shared across all services)
module containerAppsEnv 'modules/container-apps-env.bicep' = {
  name: 'container-apps-env'
  params: {
    location: location
    name: envName
    tags: tags
  }
}

// Services — each uses the reusable container-app.bicep module
var services = [
  { name: 'gateway',                  port: 8080, minReplicas: 1, maxReplicas: 3 }
  { name: 'identity',                 port: 8080, minReplicas: 1, maxReplicas: 3 }
  { name: 'tenant-management',        port: 8080, minReplicas: 0, maxReplicas: 2 }
  { name: 'billing',                  port: 8080, minReplicas: 0, maxReplicas: 2 }
  { name: 'fleet-assets',             port: 8080, minReplicas: 0, maxReplicas: 3 }
  { name: 'telemetry',                port: 8080, minReplicas: 1, maxReplicas: 10 }
  { name: 'geofencing',               port: 8080, minReplicas: 0, maxReplicas: 5 }
  { name: 'predictive-maintenance',   port: 8080, minReplicas: 0, maxReplicas: 3 }
  { name: 'reporting',                port: 8080, minReplicas: 0, maxReplicas: 2 }
  { name: 'notifications',            port: 8080, minReplicas: 0, maxReplicas: 5 }
]

module apps 'modules/container-app.bicep' = [for svc in services: {
  name: 'app-${svc.name}'
  params: {
    location: location
    tags: tags
    serviceName: svc.name
    containerAppsEnvironmentId: containerAppsEnv.outputs.environmentId
    containerRegistryLoginServer: acr.outputs.loginServer
    port: svc.port
    minReplicas: svc.minReplicas
    maxReplicas: svc.maxReplicas
    environment: environment
  }
}]

output acrLoginServer string = acr.outputs.loginServer
output containerAppsEnvironmentId string = containerAppsEnv.outputs.environmentId
