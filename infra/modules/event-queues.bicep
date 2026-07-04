// Storage account + queues for Event Grid fan-out. Each queue is one independent
// consumer's durable buffer — publish once, every queue gets its own copy (the
// SNS→SQS shape, built from cheap Azure primitives).
@description('Storage account name (3-24 lowercase alphanumeric).')
param name string
param location string

@description('Queue names to create (one per fan-out consumer).')
param queueNames array
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
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
  }
}

resource queueService 'Microsoft.Storage/storageAccounts/queueServices@2023-05-01' = {
  parent: sa
  name: 'default'
}

resource queues 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = [
  for q in queueNames: {
    parent: queueService
    name: q
  }
]

output storageAccountName string = sa.name
output storageAccountId string = sa.id
