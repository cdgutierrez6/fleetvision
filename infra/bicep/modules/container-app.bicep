param location string
param tags object
param serviceName string
param containerAppsEnvironmentId string
param containerRegistryLoginServer string
param port int = 8080
param minReplicas int = 0
param maxReplicas int = 3
param environment string

var appName = 'fv-${serviceName}-${environment}'
var imageName = '${containerRegistryLoginServer}/fleetvision/${serviceName}:latest'

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: appName
  location: location
  tags: union(tags, { service: serviceName })
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    environmentId: containerAppsEnvironmentId
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: serviceName == 'gateway'
        targetPort: port
        transport: 'http2'
        allowInsecure: false
      }
      registries: [
        {
          server: containerRegistryLoginServer
          identity: 'system'
        }
      ]
      // Secrets populated by CD pipeline via az containerapp secret set
      secrets: []
    }
    template: {
      containers: [
        {
          name: serviceName
          image: imageName
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: port
              }
              initialDelaySeconds: 15
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/ready'
                port: port
              }
              initialDelaySeconds: 10
              periodSeconds: 15
            }
          ]
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: serviceName == 'telemetry' ? [
          {
            name: 'http-scaling'
            http: {
              metadata: {
                concurrentRequests: '50'
              }
            }
          }
        ] : []
      }
    }
  }
}

output appFqdn string = app.properties.configuration.ingress.fqdn
output principalId string = app.identity.principalId
