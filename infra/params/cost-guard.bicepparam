using '../cost-guard.bicep'

param location = 'northeurope'
param projectName = 'recipelibrary'
param environment = 'test'

// Set after the first main.bicep deploy (output: containerAppName)
param containerAppName = 'ca-recipelibrary-test-example'

param budgetAmount = 5
param alertEmail = 'your.email@example.com'
