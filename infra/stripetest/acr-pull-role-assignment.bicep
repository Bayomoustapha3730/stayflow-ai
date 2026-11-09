targetScope = 'resourceGroup'

@description('Approved non-production ACR in this resource group.')
param containerRegistryName string

@description('Dedicated StripeTest image-pull identity name.')
param imagePullIdentityName string

@description('Principal ID of the dedicated StripeTest image-pull identity.')
param imagePullIdentityPrincipalId string

var acrPullRoleDefinitionId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: containerRegistryName
}

resource imagePullRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerRegistry.id, imagePullIdentityName, acrPullRoleDefinitionId)
  scope: containerRegistry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleDefinitionId)
    principalId: imagePullIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}
