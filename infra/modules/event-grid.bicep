// Event Grid custom topic (CloudEvents 1.0) — the broadcast point. The API publishes a
// minimal domain event here; subscriptions fan it out. Queue subscriptions are created
// here (storage account exists at deploy time). The webhook subscription is created
// post-deploy (`make wire-events`) once the app URL is known.
@description('Event Grid topic name.')
param name string
param location string
param tags object = {}

@description('Resource id of the storage account holding the fan-out queues.')
param storageAccountId string = ''

@description('Queue names to fan out to (one subscription each).')
param queueNames array = []

resource topic 'Microsoft.EventGrid/topics@2024-06-01-preview' = {
  name: name
  location: location
  tags: tags
  properties: {
    inputSchema: 'CloudEventSchemaV1_0'
  }
}

// One subscription per queue — publish once, each queue gets its own copy.
resource queueSubscriptions 'Microsoft.EventGrid/topics/eventSubscriptions@2024-06-01-preview' = [
  for q in queueNames: {
    parent: topic
    name: 'to-${q}'
    properties: {
      destination: {
        endpointType: 'StorageQueue'
        properties: {
          resourceId: storageAccountId
          queueName: q
        }
      }
      eventDeliverySchema: 'CloudEventSchemaV1_0'
    }
  }
]

output name string = topic.name
output endpoint string = topic.properties.endpoint
output id string = topic.id
