using '../main.bicep'

param location = 'westeurope'
param projectName = 'recipelibrary'
param environment = 'test'

// Optional: set if you want fully custom names (otherwise auto-generated stable unique suffix is used)
// param nameSuffix = 'v1'

// Fill these in for your tenant/user:
param tenantId = '00000000-0000-0000-0000-000000000000'
param entraAdminLogin = 'your.name@yourtenant.onmicrosoft.com'
param entraAdminObjectId = '00000000-0000-0000-0000-000000000000'

// Optional: set to your current public IP for laptop debugging
// param clientPublicIp = '203.0.113.10'

param allowAzureServices = true

