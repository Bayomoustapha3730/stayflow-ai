targetScope = 'resourceGroup'

@description('Deployment environment name such as staging or production.')
param environmentName string

@description('Azure location for resources.')
param location string = resourceGroup().location

@description('Immutable git SHA image tag used for promotion.')
param imageSha string

@description('Backend image reference in ACR.')
param backendImage string

@description('Frontend image reference in ACR.')
param frontendImage string

@description('Container Registry name.')
param containerRegistryName string

@description('Log Analytics workspace name.')
param logAnalyticsName string

@description('Application Insights resource name.')
param applicationInsightsName string

@description('Key Vault name.')
param keyVaultName string

@description('Container Apps environment name.')
param containerAppsEnvironmentName string

@description('Backend Container App name.')
param backendContainerAppName string

@description('Frontend Container App name.')
param frontendContainerAppName string

@description('PostgreSQL flexible server name.')
param postgresServerName string

@description('PostgreSQL database name.')
param postgresDatabaseName string = 'stayflow_ai'

@description('PostgreSQL admin user name.')
param postgresAdminUser string = 'stayflowadmin'

@secure()
@description('PostgreSQL admin password used for initial server provisioning.')
param postgresAdminPassword string

@secure()
@description('JWT signing key used by the backend.')
param jwtSigningKey string

@secure()
@description('OpenAI API key if AIProvider is configured to use OpenAI.')
param openAiApiKey string = ''

@secure()
@description('WhatsApp access token if WhatsApp integration is enabled.')
param whatsappAccessToken string = ''

@secure()
@description('WhatsApp webhook secret if WhatsApp integration is enabled.')
param whatsappWebhookSecret string = ''

@description('Allowed frontend origins for CORS.')
param allowedOrigins array = []

@description('Backend minimum replica count.')
param backendMinReplicas int = 1

@description('Backend maximum replica count.')
param backendMaxReplicas int = 3

@description('Frontend minimum replica count.')
param frontendMinReplicas int = 1

@description('Frontend maximum replica count.')
param frontendMaxReplicas int = 3

@description('Backend CPU allocation.')
param backendCpu string = '0.5'

@description('Backend memory allocation.')
param backendMemory string = '1Gi'

@description('Frontend CPU allocation.')
param frontendCpu string = '0.25'

@description('Frontend memory allocation.')
param frontendMemory string = '0.5Gi'

@description('PostgreSQL SKU name.')
param postgresSkuName string = 'Standard_D2ds_v5'

@description('PostgreSQL storage in GB.')
param postgresStorageGb int = 128

@description('PostgreSQL version.')
param postgresVersion string = '16'

@description('Whether to deploy Azure SignalR.')
param enableSignalR bool = false

@description('Azure SignalR resource name.')
param signalRName string = ''

var tags = {
  environment: environmentName
  application: 'StayFlow'
  commitSha: imageSha
  deployedBy: 'bicep'
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: environmentName == 'production' ? 30 : 14
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: applicationInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: containerRegistryName
  location: location
  tags: tags
  sku: {
    name: environmentName == 'production' ? 'Standard' : 'Basic'
  }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
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
    publicNetworkAccess: 'Enabled'
    accessPolicies: []
  }
}

resource postgresServer 'Microsoft.DBforPostgreSQL/flexibleServers@2023-06-01' = {
  name: postgresServerName
  location: location
  tags: tags
  sku: {
    name: postgresSkuName
    tier: 'GeneralPurpose'
  }
  properties: {
    administratorLogin: postgresAdminUser
    administratorLoginPassword: postgresAdminPassword
    version: postgresVersion
    storage: {
      storageSizeGB: postgresStorageGb
    }
    backup: {
      backupRetentionDays: environmentName == 'production' ? 14 : 7
      geoRedundantBackup: environmentName == 'production' ? 'Enabled' : 'Disabled'
    }
    network: {
      publicNetworkAccess: 'Enabled'
    }
  }
}

resource postgresDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-06-01' = {
  parent: postgresServer
  name: postgresDatabaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

resource signalR 'Microsoft.SignalRService/signalR@2023-02-01' = if (enableSignalR) {
  name: signalRName
  location: location
  sku: {
    name: 'Standard_S1'
    tier: 'Standard'
    capacity: 1
  }
  properties: {
    publicNetworkAccess: 'Enabled'
    features: [
      {
        flag: 'ServiceMode'
        value: 'Default'
      }
    ]
  }
}

resource containerAppsEnv 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: containerAppsEnvironmentName
  location: location
  tags: tags
  properties: {
    workloadProfiles: []
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: listKeys(logAnalytics.id, '2022-10-01').primarySharedKey
      }
    }
  }
}

resource backendApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: backendContainerAppName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppsEnv.id
    configuration: {
      activeRevisionsMode: 'single'
      ingress: {
        external: false
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        {
          server: acr.properties.loginServer
          identity: 'system'
        }
      ]
      secrets: [
        {
          name: 'connection-string'
          value: 'Host=${postgresServer.name}.postgres.database.azure.com;Port=5432;Database=${postgresDatabase.name};Username=${postgresAdminUser};Password=${postgresAdminPassword};Ssl Mode=Require'
        }
        {
          name: 'jwt-signing-key'
          value: jwtSigningKey
        }
        {
          name: 'openai-api-key'
          value: openAiApiKey
        }
        {
          name: 'whatsapp-access-token'
          value: whatsappAccessToken
        }
        {
          name: 'whatsapp-webhook-secret'
          value: whatsappWebhookSecret
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'backend'
          image: backendImage
          resources: {
            cpu: json(backendCpu)
            memory: backendMemory
          }
          env: concat(
            [
              {
                name: 'ASPNETCORE_ENVIRONMENT'
                value: 'Production'
              }
              {
                name: 'ASPNETCORE_URLS'
                value: 'http://+:8080'
              }
              {
                name: 'ConnectionStrings__DefaultConnection'
                secretRef: 'connection-string'
              }
              {
                name: 'Jwt__Issuer'
                value: 'StayFlow.Api'
              }
              {
                name: 'Jwt__Audience'
                value: 'StayFlow.Clients'
              }
              {
                name: 'Jwt__SigningKey'
                secretRef: 'jwt-signing-key'
              }
            ],
            [for (origin, index) in allowedOrigins: {
              name: 'Cors__AllowedOrigins__${index}'
              value: origin
            }],
            [for (origin, index) in allowedOrigins: {
              name: 'ProductionHardening__Security__AllowedOrigins__${index}'
              value: origin
            }]
          )
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health/live'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 15
              periodSeconds: 20
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 15
              periodSeconds: 20
            }
          ]
        }
      ]
      scale: {
        minReplicas: backendMinReplicas
        maxReplicas: backendMaxReplicas
        rules: [
          {
            name: 'http'
            http: {
              metadata: {
                concurrentRequests: '50'
              }
            }
          }
        ]
      }
    }
  }
}

resource frontendApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: frontendContainerAppName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppsEnv.id
    configuration: {
      activeRevisionsMode: 'single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        {
          server: acr.properties.loginServer
          identity: 'system'
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'frontend'
          image: frontendImage
          resources: {
            cpu: json(frontendCpu)
            memory: frontendMemory
          }
          env: [
            {
              name: 'STAYFLOW_API_URL'
              value: 'https://${backendApp.properties.configuration.ingress.fqdn}'
            }
            {
              name: 'STAYFLOW_SIGNALR_URL'
              value: 'https://${backendApp.properties.configuration.ingress.fqdn}/hubs/conversations'
            }
            {
              name: 'STAYFLOW_ENVIRONMENT'
              value: environmentName
            }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/healthz'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 10
              periodSeconds: 20
            }
          ]
        }
      ]
      scale: {
        minReplicas: frontendMinReplicas
        maxReplicas: frontendMaxReplicas
        rules: [
          {
            name: 'http'
            http: {
              metadata: {
                concurrentRequests: '80'
              }
            }
          }
        ]
      }
    }
  }
}

output backendFqdn string = backendApp.properties.configuration.ingress.fqdn
output frontendFqdn string = frontendApp.properties.configuration.ingress.fqdn
output containerRegistryLoginServer string = acr.properties.loginServer
output keyVaultUri string = keyVault.properties.vaultUri
output postgresHost string = '${postgresServer.name}.postgres.database.azure.com'
