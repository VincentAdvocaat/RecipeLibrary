## Infrastructure (Bicep)

This folder contains Bicep templates to provision a **test** environment on Azure:

- **App Service Plan**: Free (F1)
- **Web App**: System Assigned Managed Identity enabled
- **Azure SQL**: General Purpose **serverless** database using the **Azure SQL Database Free Offer**
  - `useFreeLimit: true`
  - `freeLimitExhaustionBehavior: AutoPause` (auto-pauses until next month when the free limit is exhausted)

### Files

- `subscription.bicep`: creates the resource group (subscription-scope)
- `main.bicep`: deploys resources into the resource group (resource-group scope)
- `params/test.bicepparam`: example parameter file for local/manual deploy

### Pipeline deploy

CI/CD on `main` deploys `main.bicep` into an existing resource group via
`azure-pipelines.yml`. Create the group first (`subscription.bicep` is for
manual/local use only). Entra parameters come from Azure DevOps variables — see
`docs/azure/pipeline-setup.md`.

