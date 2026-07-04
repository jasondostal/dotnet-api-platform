// Container Apps managed environment (Consumption). Apps in it scale to zero — ~$0 at
// rest. Container logs flow to the shared Log Analytics workspace.
@description('Managed environment name.')
param name string
param location string

@description('Log Analytics workspace name (for container log shipping).')
param logAnalyticsWorkspaceName string
param tags object = {}

resource ws 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: logAnalyticsWorkspaceName
}

resource env 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: ws.properties.customerId
        sharedKey: ws.listKeys().primarySharedKey
      }
    }
  }
}

output name string = env.name
output id string = env.id
