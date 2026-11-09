targetScope = 'resourceGroup'

@description('Immutable commit SHA for both application images.')
@minLength(40)
@maxLength(40)
param imageSha string

@description('Backend image in the approved non-production ACR, pinned by SHA-256 digest.')
param backendImage string

@description('Frontend image in the approved non-production ACR, pinned by SHA-256 digest.')
param frontendImage string

@description('Approved non-production ACR name.')
param containerRegistryName string

@description('Resource group containing the approved non-production ACR.')
param containerRegistryResourceGroupName string

@description('Dedicated Container Apps environment created by main.bicep.')
param containerAppsEnvironmentName string

@description('Dedicated Key Vault created by main.bicep.')
param keyVaultName string

@description('Dedicated Application Insights component created by main.bicep.')
param applicationInsightsName string

@description('Dedicated runtime identity created by main.bicep.')
param runtimeIdentityName string

@description('Dedicated ACR image-pull identity created by main.bicep.')
param imagePullIdentityName string

@description('Dedicated PostgreSQL Flexible Server created by main.bicep.')
param postgresServerName string

@description('Dedicated StripeTest database created by main.bicep.')
param postgresDatabaseName string

@description('Dedicated backend Container App.')
param backendContainerAppName string

@description('Dedicated frontend Container App.')
param frontendContainerAppName string

@description('Runtime secret names that must be provisioned in the dedicated Key Vault before app deployment.')
param databaseConnectionSecretName string = 'stripetest-database-connection'
param jwtSigningKeySecretName string = 'stripetest-jwt-signing-key'
param stripeSecretKeySecretName string = 'stripetest-stripe-secret-key'
param stripeWebhookSecretName string = 'stripetest-stripe-webhook-signing-secret'
param starterPriceIdSecretName string = 'stripetest-starter-price-id'
param professionalPriceIdSecretName string = 'stripetest-professional-price-id'

var tags = {
  application: 'StayFlow'
  environment: 'StripeTest'
  managedBy: 'Bicep'
  commitSha: imageSha
}

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2023-05-01' existing = {
  name: containerAppsEnvironmentName
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource runtimeIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: runtimeIdentityName
}

resource imagePullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: imagePullIdentityName
}

resource postgresServer 'Microsoft.DBforPostgreSQL/flexibleServers@2023-06-01' existing = {
  name: postgresServerName
}

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: containerRegistryName
  scope: resourceGroup(containerRegistryResourceGroupName)
}

var frontendOrigin = 'https://${frontendContainerAppName}.${containerAppsEnvironment.properties.defaultDomain}'
var postgresHost = '${postgresServer.name}.postgres.database.azure.com'

resource backendApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: backendContainerAppName
  location: resourceGroup().location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${runtimeIdentity.id}': {}
      '${imagePullIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
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
          server: containerRegistry.properties.loginServer
          identity: imagePullIdentity.id
        }
      ]
      secrets: [
        {
          name: 'database-connection'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/${databaseConnectionSecretName}'
          identity: runtimeIdentity.id
        }
        {
          name: 'jwt-signing-key'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/${jwtSigningKeySecretName}'
          identity: runtimeIdentity.id
        }
        {
          name: 'stripe-secret-key'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/${stripeSecretKeySecretName}'
          identity: runtimeIdentity.id
        }
        {
          name: 'stripe-webhook-secret'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/${stripeWebhookSecretName}'
          identity: runtimeIdentity.id
        }
        {
          name: 'starter-price-id'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/${starterPriceIdSecretName}'
          identity: runtimeIdentity.id
        }
        {
          name: 'professional-price-id'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/${professionalPriceIdSecretName}'
          identity: runtimeIdentity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'backend'
          image: backendImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'StayFlow__EnvironmentIdentity'
              value: 'StripeTest'
            }
            {
              name: 'StayFlow__StripeTest__Database__ApprovedHost'
              value: postgresHost
            }
            {
              name: 'StayFlow__StripeTest__Database__ApprovedDatabase'
              value: postgresDatabaseName
            }
            {
              name: 'ConnectionStrings__DefaultConnection'
              secretRef: 'database-connection'
            }
            {
              name: 'Jwt__SigningKey'
              secretRef: 'jwt-signing-key'
            }
            {
              name: 'Billing__Provider'
              value: 'Stripe'
            }
            {
              name: 'Billing__StripeTestModeExpected'
              value: 'true'
            }
            {
              name: 'Billing__StripeSecretKey'
              secretRef: 'stripe-secret-key'
            }
            {
              name: 'Billing__StripeWebhookSigningSecret'
              secretRef: 'stripe-webhook-secret'
            }
            {
              name: 'Billing__StripeApiVersion'
              value: '2024-06-20'
            }
            {
              name: 'Billing__PlanPriceIds__Starter'
              secretRef: 'starter-price-id'
            }
            {
              name: 'Billing__PlanPriceIds__Professional'
              secretRef: 'professional-price-id'
            }
            {
              name: 'Billing__UsageBilling__Enabled'
              value: 'false'
            }
            {
              name: 'Billing__UsageBilling__Worker__Enabled'
              value: 'false'
            }
            {
              name: 'Billing__UsageBilling__OpenPeriodInvoiceSafetyVerified'
              value: 'false'
            }
            {
              name: 'Billing__CheckoutSuccessUrl'
              value: '${frontendOrigin}/host/settings/billing?checkout=success'
            }
            {
              name: 'Billing__CheckoutCancelUrl'
              value: '${frontendOrigin}/host/settings/billing?checkout=cancel'
            }
            {
              name: 'Billing__BillingPortalReturnUrl'
              value: '${frontendOrigin}/host/settings/billing'
            }
            {
              name: 'Cors__AllowedOrigins__0'
              value: frontendOrigin
            }
            {
              name: 'ProductionHardening__Security__AllowedOrigins__0'
              value: frontendOrigin
            }
            {
              name: 'ProductionHardening__Security__AllowLocalOrigins'
              value: 'false'
            }
            {
              name: 'WhatsAppCloud__Enabled'
              value: 'false'
            }
            {
              name: 'WhatsAppCloud__ProductionSendingEnabled'
              value: 'false'
            }
            {
              name: 'WhatsAppCloud__DevelopmentMode'
              value: 'false'
            }
            {
              name: 'Mpesa__Enabled'
              value: 'false'
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: applicationInsights.properties.ConnectionString
            }
          ]
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
        minReplicas: 0
        maxReplicas: 2
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

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: applicationInsightsName
}

resource frontendApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: frontendContainerAppName
  location: resourceGroup().location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${imagePullIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
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
          server: containerRegistry.properties.loginServer
          identity: imagePullIdentity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'frontend'
          image: frontendImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
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
              value: 'stripetest'
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
        minReplicas: 0
        maxReplicas: 1
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
