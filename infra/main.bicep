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

var stableSuffix = toLower(uniqueString(subscription().subscriptionId, resourceGroup().id))
var suffix = (nameSuffix == '') ? stableSuffix : toLower(nameSuffix)

var webAppName = toLower('${projectName}-${environment}-${suffix}')
var planName = toLower('asp-${projectName}-${environment}-${suffix}')

// SQL logical server names must be lowercase, alphanumeric, and hyphen; max 63.
var sqlServerName = toLower('sql-${projectName}-${environment}-${suffix}')
var sqlDatabaseName = toLower('${projectName}-${environment}')

var sqlFqdn = '${sqlServerName}.database.windows.net'

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  sku: {
    name: 'F1'
    tier: 'Free'
    capacity: 1
  }
  properties: {
    reserved: false
  }
}

resource web 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    httpsOnly: true
    serverFarmId: plan.id
    siteConfig: {
      http20Enabled: true
      alwaysOn: false
    }
  }
}

resource webAppSettings 'Microsoft.Web/sites/config@2023-12-01' = {
  name: '${web.name}/appsettings'
  properties: {
    ASPNETCORE_ENVIRONMENT: 'test'
    WEBSITE_RUN_FROM_PACKAGE: '1'
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
    // Serverless settings
    minCapacity: json('0.5')
    autoPauseDelay: 60
    maxSizeBytes: 34359738368 // 32 GiB

    // Free Offer settings
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

// Connection string uses Managed Identity. No passwords.
resource webConnStrings 'Microsoft.Web/sites/config@2023-12-01' = {
  name: '${web.name}/connectionstrings'
  properties: {
    RecipeDb: {
      value: 'Server=tcp:${sqlFqdn},1433;Database=${sqlDatabaseName};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;'
      type: 'SQLAzure'
    }
  }
}

output webAppName string = web.name
output webAppDefaultHostname string = web.properties.defaultHostName
output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlFqdn
output sqlDatabaseName string = sqlDb.name
output webAppManagedIdentityPrincipalId string = web.identity.principalId

