targetScope = 'resourceGroup'

@description('Approved region for the dedicated StripeTest resource group.')
param location string

@description('Dedicated StripeTest Container Apps environment.')
param containerAppsEnvironmentName string

@description('Dedicated StripeTest PostgreSQL Flexible Server.')
param postgresServerName string

@description('Dedicated StripeTest database; this must not be a production database.')
param postgresDatabaseName string

@description('Provisioning-only PostgreSQL administrator login. Never use this as the application login.')
param postgresAdminUser string

@secure()
@description('Provisioning-only PostgreSQL administrator password.')
param postgresAdminPassword string

@description('Dedicated StripeTest Key Vault.')
param keyVaultName string

@description('Dedicated StripeTest Log Analytics workspace.')
param logAnalyticsName string

@description('Dedicated StripeTest Application Insights component.')
param applicationInsightsName string

@description('Dedicated StripeTest virtual network.')
param virtualNetworkName string

@description('Approved, non-overlapping VNet CIDR. Replace the placeholder after network review.')
param virtualNetworkAddressPrefix string

@description('Approved subnet CIDR for Container Apps infrastructure.')
param containerAppsSubnetAddressPrefix string

@description('Approved delegated subnet CIDR for PostgreSQL Flexible Server.')
param postgresSubnetAddressPrefix string

@description('Approved subnet CIDR for the Key Vault private endpoint.')
param privateEndpointSubnetAddressPrefix string

@description('PostgreSQL Flexible Server private DNS zone name.')
param postgresPrivateDnsZoneName string = 'private.postgres.database.azure.com'

@description('Dedicated StripeTest runtime user-assigned identity.')
param runtimeIdentityName string

@description('Dedicated image-pull user-assigned identity.')
param imagePullIdentityName string

@description('Approved non-production ACR name in this subscription.')
param containerRegistryName string

@description('Resource group containing the approved non-production ACR.')
param containerRegistryResourceGroupName string

@description('PostgreSQL Flexible Server SKU; confirm availability in the approved region.')
param postgresSkuName string = 'Standard_B1ms'

@minValue(32)
@description('PostgreSQL storage size in GB; backup storage may incur additional cost.')
param postgresStorageGb int = 32

@description('Container Apps / PostgreSQL subnet names are internal deployment identifiers.')
param containerAppsSubnetName string = 'stripetest-aca-subnet'

param postgresSubnetName string = 'stripetest-postgres-subnet'
param privateEndpointSubnetName string = 'stripetest-private-endpoint-subnet'

var tags = {
  application: 'StayFlow'
  environment: 'StripeTest'
  managedBy: 'Bicep'
}

var acrPullRoleDefinitionId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'
var keyVaultSecretsUserRoleDefinitionId = '4633458b-17de-408a-b874-0445c86b69e6'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 7
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: applicationInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2023-05-01' = {
  name: virtualNetworkName
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        virtualNetworkAddressPrefix
      ]
    }
  }
}

resource containerAppsSubnet 'Microsoft.Network/virtualNetworks/subnets@2023-05-01' = {
  parent: virtualNetwork
  name: containerAppsSubnetName
  properties: {
    addressPrefix: containerAppsSubnetAddressPrefix
    delegations: [
      {
        name: 'container-apps-delegation'
        properties: {
          serviceName: 'Microsoft.App/environments'
        }
      }
    ]
  }
}

resource postgresSubnet 'Microsoft.Network/virtualNetworks/subnets@2023-05-01' = {
  parent: virtualNetwork
  name: postgresSubnetName
  properties: {
    addressPrefix: postgresSubnetAddressPrefix
    delegations: [
      {
        name: 'postgres-flexible-server-delegation'
        properties: {
          serviceName: 'Microsoft.DBforPostgreSQL/flexibleServers'
        }
      }
    ]
  }
}

resource privateEndpointSubnet 'Microsoft.Network/virtualNetworks/subnets@2023-05-01' = {
  parent: virtualNetwork
  name: privateEndpointSubnetName
  properties: {
    addressPrefix: privateEndpointSubnetAddressPrefix
    privateEndpointNetworkPolicies: 'Disabled'
  }
}

resource postgresPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: postgresPrivateDnsZoneName
  location: 'global'
  tags: tags
}

resource postgresPrivateDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: postgresPrivateDnsZone
  name: '${virtualNetworkName}-postgres-link'
  location: 'global'
  properties: {
    virtualNetwork: {
      id: virtualNetwork.id
    }
    registrationEnabled: false
  }
}

resource postgresServer 'Microsoft.DBforPostgreSQL/flexibleServers@2023-06-01' = {
  name: postgresServerName
  location: location
  tags: tags
  sku: {
    name: postgresSkuName
    tier: 'Burstable'
  }
  properties: {
    administratorLogin: postgresAdminUser
    administratorLoginPassword: postgresAdminPassword
    version: '16'
    storage: {
      storageSizeGB: postgresStorageGb
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    network: {
      publicNetworkAccess: 'Disabled'
      delegatedSubnetResourceId: postgresSubnet.id
      privateDnsZoneArmResourceId: postgresPrivateDnsZone.id
    }
  }
  dependsOn: [
    postgresPrivateDnsLink
  ]
}

resource postgresDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-06-01' = {
  parent: postgresServer
  name: postgresDatabaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enablePurgeProtection: true
    enableSoftDelete: true
    publicNetworkAccess: 'Disabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Deny'
      ipRules: []
      virtualNetworkRules: []
    }
    accessPolicies: []
  }
}

resource keyVaultPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: 'privatelink.vaultcore.azure.net'
  location: 'global'
  tags: tags
}

resource keyVaultPrivateDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: keyVaultPrivateDnsZone
  name: '${virtualNetworkName}-keyvault-link'
  location: 'global'
  properties: {
    virtualNetwork: {
      id: virtualNetwork.id
    }
    registrationEnabled: false
  }
}

resource keyVaultPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-05-01' = {
  name: '${keyVaultName}-private-endpoint'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: privateEndpointSubnet.id
    }
    privateLinkServiceConnections: [
      {
        name: 'key-vault-private-link'
        properties: {
          privateLinkServiceId: keyVault.id
          groupIds: [
            'vault'
          ]
        }
      }
    ]
  }
}

resource keyVaultPrivateDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-05-01' = {
  parent: keyVaultPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'key-vault'
        properties: {
          privateDnsZoneId: keyVaultPrivateDnsZone.id
        }
      }
    ]
  }
  dependsOn: [
    keyVaultPrivateDnsLink
  ]
}

resource runtimeIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: runtimeIdentityName
  location: location
  tags: tags
}

resource imagePullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: imagePullIdentityName
  location: location
  tags: tags
}

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: containerRegistryName
  scope: resourceGroup(containerRegistryResourceGroupName)
}

module imagePullRoleAssignment './acr-pull-role-assignment.bicep' = {
  name: guid(subscription().id, containerRegistryResourceGroupName, containerRegistryName, imagePullIdentityName, acrPullRoleDefinitionId)
  scope: resourceGroup(containerRegistryResourceGroupName)
  params: {
    containerRegistryName: containerRegistryName
    imagePullIdentityName: imagePullIdentityName
    imagePullIdentityPrincipalId: imagePullIdentity.properties.principalId
  }
}

resource keyVaultSecretsRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, runtimeIdentityName, keyVaultSecretsUserRoleDefinitionId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleDefinitionId)
    principalId: runtimeIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: containerAppsEnvironmentName
  location: location
  tags: tags
  properties: {
    workloadProfiles: []
    vnetConfiguration: {
      infrastructureSubnetId: containerAppsSubnet.id
      internal: false
    }
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: listKeys(logAnalytics.id, '2022-10-01').primarySharedKey
      }
    }
  }
  dependsOn: [
    keyVaultPrivateEndpoint
    postgresServer
  ]
}

output postgresHost string = '${postgresServer.name}.postgres.database.azure.com'
output postgresDatabaseName string = postgresDatabase.name
output keyVaultUri string = keyVault.properties.vaultUri
output runtimeIdentityId string = runtimeIdentity.id
output imagePullIdentityId string = imagePullIdentity.id
output containerAppsEnvironmentId string = containerAppsEnvironment.id
output containerRegistryLoginServer string = containerRegistry.properties.loginServer
