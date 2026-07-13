targetScope = 'subscription'

@description('Azure region for the resource group.')
param location string = 'northeurope'

@description('Project name used for naming (lowercase recommended).')
param projectName string = 'recipelibrary'

@description('Environment name, e.g. test.')
param environment string = 'test'

@description('Resource group name. Defaults to rg-<project>-<env>-neu.')
param resourceGroupName string = 'rg-${projectName}-${environment}-neu'

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
}

output resourceGroupId string = rg.id
output resourceGroupName string = rg.name

