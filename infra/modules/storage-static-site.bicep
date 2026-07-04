// Storage account for the internal dev portal (static website). The $web container
// + static-website feature are enabled post-deploy by the Makefile (`make portal`),
// which also uploads the Redocly build output. Bicep can create the account but does
// not toggle the static-website feature declaratively.
@description('Storage account name (3-24 lowercase alphanumeric).')
param name string
param location string
param tags object = {}

resource sa 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: true
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

output accountName string = sa.name
output blobEndpoint string = sa.properties.primaryEndpoints.blob
