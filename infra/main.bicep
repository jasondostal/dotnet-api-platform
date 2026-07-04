// ─────────────────────────────────────────────────────────────────────────────
// dotnet-api-platform — PoC infrastructure (self-contained, resource-group scoped).
//
//   Container Registry (Basic) · Container Apps environment (Consumption,
//   scale-to-zero) · Log Analytics + Application Insights (workspace-based) ·
//   Blob static-website portal.
//
// This template provisions the PLATFORM. The API container app itself is created by
// `make deploy` (build image in ACR → create/update the app), which keeps image
// lifecycle out of Bicep and enables blue/green via Container Apps revisions.
//
//   make up      # deploy this platform
//   make deploy  # build image + create/update the app (blue/green revisions)
//   make down    # delete the whole RG (scales to ~$0 at rest anyway)
// ─────────────────────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

@description('Short resource name prefix.')
param namePrefix string = 'apip'

@description('Azure region. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('Environment tag.')
param environment string = 'dev'

var tags = {
  workload: 'dotnet-api-platform'
  environment: environment
}

var suffix = uniqueString(resourceGroup().id)

module logs 'modules/log-analytics.bicep' = {
  name: 'logs'
  params: {
    name: '${namePrefix}-logs'
    location: location
    tags: tags
  }
}

module ai 'modules/app-insights.bicep' = {
  name: 'appinsights'
  params: {
    name: '${namePrefix}-ai'
    location: location
    workspaceId: logs.outputs.id
    tags: tags
  }
}

module acr 'modules/container-registry.bicep' = {
  name: 'acr'
  params: {
    name: toLower(replace('${namePrefix}acr${suffix}', '-', ''))
    location: location
    tags: tags
  }
}

module env 'modules/container-app-env.bicep' = {
  name: 'containerenv'
  params: {
    name: '${namePrefix}-env'
    location: location
    logAnalyticsWorkspaceName: logs.outputs.name
    tags: tags
  }
}

module portal 'modules/storage-static-site.bicep' = {
  name: 'portal'
  params: {
    name: toLower('${namePrefix}portal${suffix}')
    location: location
    tags: tags
  }
}

var eventQueueNames = [
  'sink-a'
  'sink-b'
]

module eventq 'modules/event-queues.bicep' = {
  name: 'eventqueues'
  params: {
    name: toLower('${namePrefix}evt${suffix}')
    location: location
    queueNames: eventQueueNames
    tags: tags
  }
}

module egrid 'modules/event-grid.bicep' = {
  name: 'eventgrid'
  params: {
    name: '${namePrefix}-topic'
    location: location
    storageAccountId: eventq.outputs.storageAccountId
    queueNames: eventQueueNames
    tags: tags
  }
}

output acrName string = acr.outputs.name
output acrLoginServer string = acr.outputs.loginServer
output containerEnvName string = env.outputs.name
output appInsightsConnectionString string = ai.outputs.connectionString
output portalStorageAccount string = portal.outputs.accountName
output appName string = '${namePrefix}-api'
output eventGridTopicName string = egrid.outputs.name
output eventGridEndpoint string = egrid.outputs.endpoint
output eventsStorageAccount string = eventq.outputs.storageAccountName
