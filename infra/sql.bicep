targetScope = 'resourceGroup'

param location string
param sqlServerName string
param sqlDatabaseName string
param tenantId string
param entraAdminLogin string
param entraAdminObjectId string
param allowAzureServices bool = true
param clientPublicIp string = ''
param useBasicSql bool = false

var basicSku = {
  name: 'Basic'
  tier: 'Basic'
  capacity: 5
}
var serverlessSku = {
  name: 'GP_S_Gen5'
  tier: 'GeneralPurpose'
  family: 'Gen5'
  capacity: 2
}
var basicProperties = {
  maxSizeBytes: 2147483648
  useFreeLimit: false
}
var serverlessProperties = {
  minCapacity: json('0.5')
  autoPauseDelay: 60
  maxSizeBytes: 34359738368
  useFreeLimit: true
  freeLimitExhaustionBehavior: 'AutoPause'
}

// Explicit SQL provisioning only. Reapplying database properties can resume serverless SQL.
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
  sku: useBasicSql ? basicSku : serverlessSku
  properties: useBasicSql ? basicProperties : serverlessProperties
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

