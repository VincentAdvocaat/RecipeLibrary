targetScope = 'resourceGroup'

@description('Azure region for resources. Usually matches the resource group location.')
param location string = resourceGroup().location

@description('Project name used for naming (lowercase recommended).')
param projectName string = 'recipelibrary'

@description('Environment name, e.g. test.')
param environment string = 'test'

@description('Optional suffix for globally-unique resource names. If empty, a stable unique suffix is generated.')
param nameSuffix string = ''

@description('Your tenant id (Entra ID tenant).')
param tenantId string

@description('Entra admin login (display name or UPN) for the Azure SQL logical server.')
param entraAdminLogin string

@description('Entra admin object id (GUID) for the Azure SQL logical server.')
param entraAdminObjectId string

@description('Optional public IPv4 address to allow for laptop debugging (e.g. 203.0.113.10). If empty, no IP rule is added.')
param clientPublicIp string = ''

@description('Whether to allow Azure services to access the SQL server (equivalent to adding firewall rule 0.0.0.0).')
param allowAzureServices bool = true

@description('GHCR image repository without digest (lowercase), e.g. ghcr.io/owner/recipelibrary.')
param ghcrImageRepository string = 'ghcr.io/vincentadvocaat/recipelibrary'

@description('Immutable GHCR image digest including sha256 prefix, e.g. sha256:abc123....')
param containerImageDigest string

@secure()
@description('GitHub username used by Container Apps to authenticate to GHCR.')
param ghcrUsername string

@secure()
@description('GitHub PAT (or token) with read:packages used by Container Apps to pull from GHCR.')
param ghcrPassword string

var stableSuffix = toLower(uniqueString(subscription().subscriptionId, resourceGroup().id))
var suffix = (nameSuffix == '') ? stableSuffix : toLower(nameSuffix)

// Container App names are limited to 2-32 chars (alphanumeric + single hyphens).
// Abbreviate the project segment and shorten the unique suffix so env names like test-neu fit.
var containerAppShortSuffix = take(suffix, 6)
var containerAppName = toLower('ca-rl-${environment}-${containerAppShortSuffix}')
var managedEnvName = toLower('cae-${projectName}-${environment}-${suffix}')
var appIdentityName = toLower('id-${projectName}-${environment}-${suffix}')

// Storage account names: 3-24 chars, lowercase alphanumeric. Bicep substring length is capped at 13.
var storageAccountName = toLower('st${substring(uniqueString(resourceGroup().id, projectName, environment, 'blob'), 0, 13)}')

// SQL logical server names must be lowercase, alphanumeric, and hyphen; max 63.
var sqlServerName = toLower('sql-${projectName}-${environment}-${suffix}')
var sqlDatabaseName = toLower('${projectName}-${environment}')

var sqlFqdn = '${sqlServerName}.database.windows.net'
var containerImage = '${ghcrImageRepository}@${containerImageDigest}'
var dataProtectionBlobUri = 'https://${storageAccount.name}.blob.core.windows.net/dataprotection/keys.xml'

resource appIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: appIdentityName
  location: location
}

resource managedEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: managedEnvName
  location: location
  properties: {}
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
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

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource recipeImagesContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'recipe-images'
  properties: {
    publicAccess: 'None'
  }
}

resource dataProtectionContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'dataprotection'
  properties: {
    publicAccess: 'None'
  }
}

var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

resource appBlobContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, appIdentity.id, storageBlobDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: appIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource sqlServer 'Microsoft.Sql/servers@2024-05-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: entraAdminLogin
      sid: entraAdminObjectId
      tenantId: tenantId
      principalType: 'User'
    }
  }
}

resource sqlDb 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  name: sqlDatabaseName
  parent: sqlServer
  location: location
  sku: {
    name: 'GP_S_Gen5'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 2
  }
  properties: {
    minCapacity: json('0.5')
    autoPauseDelay: 60
    maxSizeBytes: 34359738368 // 32 GiB
    useFreeLimit: true
    freeLimitExhaustionBehavior: 'AutoPause'
  }
}

resource sqlAllowAzure 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = if (allowAzureServices) {
  name: 'AllowAzureServices'
  parent: sqlServer
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlAllowClientIp 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = if (clientPublicIp != '') {
  name: 'AllowClientIp'
  parent: sqlServer
  properties: {
    startIpAddress: clientPublicIp
    endIpAddress: clientPublicIp
  }
}

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${appIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: managedEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      // GHCR packages are private by default; Container Apps need registry credentials to pull.
      secrets: [
        {
          name: 'ghcr-password'
          value: ghcrPassword
        }
      ]
      registries: [
        {
          server: 'ghcr.io'
          username: ghcrUsername
          passwordSecretRef: 'ghcr-password'
        }
      ]
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
        stickySessions: {
          affinity: 'sticky'
        }
      }
    }
    template: {
      containers: [
        {
          name: 'web'
          image: containerImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'test'
            }
            {
              name: 'SKIP_SQL_WAIT'
              value: 'true'
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: appIdentity.properties.clientId
            }
            {
              name: 'ConnectionStrings__RecipeDb'
              value: 'Server=tcp:${sqlFqdn},1433;Database=${sqlDatabaseName};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;User Id=${appIdentity.properties.clientId};'
            }
            {
              name: 'RecipeFileStorage__Provider'
              value: 'AzureBlob'
            }
            {
              name: 'RecipeFileStorage__AzureBlob__AccountName'
              value: storageAccount.name
            }
            {
              name: 'RecipeFileStorage__AzureBlob__ContainerName'
              value: recipeImagesContainer.name
            }
            {
              name: 'DataProtection__ApplicationName'
              value: '${projectName}-${environment}'
            }
            {
              name: 'DataProtection__BlobUri'
              value: dataProtectionBlobUri
            }
          ]
          probes: [
            {
              type: 'Startup'
              httpGet: {
                path: '/health'
                port: 8080
                scheme: 'HTTP'
              }
              periodSeconds: 10
              failureThreshold: 30
            }
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: 8080
                scheme: 'HTTP'
              }
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health'
                port: 8080
                scheme: 'HTTP'
              }
              periodSeconds: 10
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 1
        rules: [
          {
            name: 'http-scaler'
            http: {
              metadata: {
                concurrentRequests: '10'
              }
            }
          }
        ]
      }
    }
  }
}

output containerAppName string = containerApp.name
output containerAppFqdn string = containerApp.properties.configuration.ingress.fqdn
output managedIdentityName string = appIdentity.name
output managedIdentityPrincipalId string = appIdentity.properties.principalId
output managedIdentityClientId string = appIdentity.properties.clientId
output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlFqdn
output sqlDatabaseName string = sqlDb.name
output storageAccountName string = storageAccount.name
output recipeImagesContainerName string = recipeImagesContainer.name
output dataProtectionContainerName string = dataProtectionContainer.name
