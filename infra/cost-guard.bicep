targetScope = 'resourceGroup'

@description('Azure region for resources. Usually matches the resource group location.')
param location string = resourceGroup().location

@description('Project name used for naming (lowercase recommended).')
param projectName string = 'recipelibrary'

@description('Environment name, e.g. test.')
param environment string = 'test'

@description('Optional suffix for globally-unique resource names. If empty, a stable unique suffix is generated.')
param nameSuffix string = ''

@description('Existing Container App name to stop when the budget threshold is reached.')
param containerAppName string

@description('Monthly budget amount in the subscription billing currency.')
param budgetAmount int = 5

@description('Email address for budget warning notifications.')
param alertEmail string

@description('Budget period start date (YYYY-MM-01). Defaults to the first day of the current UTC month at deploy time.')
param budgetStartDate string = utcNow('yyyy-MM-01')

var stableSuffix = toLower(uniqueString(subscription().subscriptionId, resourceGroup().id))
var suffix = (nameSuffix == '') ? stableSuffix : toLower(nameSuffix)

var budgetName = toLower('budget-${projectName}-${environment}-${suffix}')
var actionGroupName = toLower('ag-costguard-${suffix}')
var logicAppName = toLower('la-costguard-${suffix}')

var containerAppsContributorRoleId = '59998874-151c-4733-baea-4d096bc06595'

resource containerApp 'Microsoft.App/containerApps@2024-03-01' existing = {
  name: containerAppName
}

var containerAppStopUri = 'https://management.azure.com${containerApp.id}/stop?api-version=2024-03-01'

resource logicApp 'Microsoft.Logic/workflows@2019-05-01' = {
  name: logicAppName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    state: 'Enabled'
    definition: {
      '$schema': 'https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#'
      contentVersion: '1.0.0.0'
      parameters: {}
      triggers: {
        manual: {
          type: 'Request'
          kind: 'Http'
          inputs: {
            schema: {
              type: 'object'
              properties: {}
            }
          }
        }
      }
      actions: {
        Stop_Container_App: {
          type: 'Http'
          runAfter: {}
          inputs: {
            method: 'POST'
            uri: containerAppStopUri
            authentication: {
              type: 'ManagedServiceIdentity'
              audience: 'https://management.azure.com'
            }
          }
        }
      }
      outputs: {}
    }
  }
}

resource logicAppContainerAppsContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerApp.id, logicApp.id, containerAppsContributorRoleId)
  scope: containerApp
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', containerAppsContributorRoleId)
    principalId: logicApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: actionGroupName
  location: 'Global'
  properties: {
    groupShortName: substring(actionGroupName, 0, 12)
    enabled: true
    emailReceivers: [
      {
        name: 'BudgetWarnings'
        emailAddress: alertEmail
        useCommonAlertSchema: false
      }
    ]
    logicAppReceivers: [
      {
        name: 'StopContainerApp'
        resourceId: logicApp.id
        callbackUrl: listCallbackUrl(logicApp.id, 'manual').value
        useCommonAlertSchema: false
      }
    ]
  }
}

resource budget 'Microsoft.Consumption/budgets@2024-08-01' = {
  name: budgetName
  properties: {
    category: 'Cost'
    amount: budgetAmount
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: budgetStartDate
      endDate: '2099-12-31'
    }
    filter: {
      dimensions: {
        name: 'ResourceGroupName'
        operator: 'In'
        values: [
          resourceGroup().name
        ]
      }
    }
    notifications: {
      Actual_GreaterThan_50_Percent: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 50
        contactEmails: [
          alertEmail
        ]
        contactGroups: []
        contactRoles: []
        thresholdType: 'Actual'
      }
      Actual_GreaterThan_80_Percent: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 80
        contactEmails: []
        contactGroups: [
          actionGroup.id
        ]
        contactRoles: []
        thresholdType: 'Actual'
      }
      Actual_GreaterThan_100_Percent: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 100
        contactEmails: [
          alertEmail
        ]
        contactGroups: []
        contactRoles: []
        thresholdType: 'Actual'
      }
      Forecast_GreaterThan_100_Percent: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 100
        contactEmails: [
          alertEmail
        ]
        contactGroups: []
        contactRoles: []
        thresholdType: 'Forecasted'
      }
    }
  }
}

output budgetName string = budget.name
output actionGroupName string = actionGroup.name
output logicAppName string = logicApp.name
output containerAppName string = containerApp.name
