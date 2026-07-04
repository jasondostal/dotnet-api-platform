@description('Application Insights component name.')
param name string
param location string

@description('Resource id of the Log Analytics workspace (workspace-based AI).')
param workspaceId string
param tags object = {}

resource ai 'Microsoft.Insights/components@2020-02-02' = {
  name: name
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspaceId
    IngestionMode: 'LogAnalytics'
  }
}

output id string = ai.id
output connectionString string = ai.properties.ConnectionString
