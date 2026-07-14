## Infrastructure (Bicep)

This folder contains Bicep templates to provision a **test** environment on Azure:

- **Container Apps Consumption**: external ingress on port `8080`, `minReplicas: 0`, `maxReplicas: 1`, sticky sessions
- **User-assigned managed identity**: stable SQL/Blob identity across revisions
- **Public GHCR image**: immutable digest parameter (anonymous pull at runtime)
- **Azure SQL**: General Purpose **serverless** database using the **Azure SQL Database Free Offer**
  - `useFreeLimit: true`
  - `freeLimitExhaustionBehavior: AutoPause` (auto-pauses until next month when the free limit is exhausted)
- **Blob Storage**: recipe images + Data Protection key ring (private containers)

### Files

- `subscription.bicep`: creates the resource group (subscription-scope)
- `main.bicep`: deploys Container App, SQL, storage into the resource group (resource-group scope)
- `cost-guard.bicep`: monthly budget, Action Group, Logic App auto-stop at 80% (deploy separately with Owner)
- `params/test.bicepparam`: example parameter file for local/manual deploy
- `params/cost-guard.bicepparam`: example parameters for cost guard deploy

### Pipeline deploy

CI/CD on `main` deploys `main.bicep` into an existing resource group via
`azure-pipelines.yml`. The pipeline builds and pushes a public GHCR image, resolves
its digest, and passes `containerImageDigest` to Bicep.

Create the group first (`subscription.bicep` is for manual/local use only). Entra
parameters and GHCR credentials come from Azure DevOps variables — see
`docs/azure/pipeline-setup.md`.

### One-time provider registration

Before the first Container Apps deploy:

```bash
az provider register --namespace Microsoft.App
az provider register --namespace Microsoft.OperationalInsights
```

Also register `Microsoft.Sql` and `Microsoft.Storage` (see
`docs/azure/subscription-bootstrap.md`).

Verify Container Apps quota in `swedencentral` if deploy fails with quota errors.

### Cost guard deploy

Deploy **`cost-guard.bicep` once** with an Owner account (not the pipeline service
principal):

```bash
az deployment group create \
  -g rg-recipelibrary-test-sec \
  --template-file infra/cost-guard.bicep \
  --parameters infra/params/cost-guard.bicepparam
```

Set `containerAppName` to the `containerAppName` output from `main.bicep` and
`alertEmail` to your notification address.

The template provisions:

- €5 monthly budget (subscription billing currency) scoped to the resource group
- 50% actual-cost email warning
- 80% actual-cost Action Group → Logic App → Container App **stop** (system-assigned MI)
- 100% actual-cost and 100% forecast email escalations
- **Container Apps Contributor** on the single Container App for the Logic App only

Cost ingestion is delayed; stopping ACA halts compute while tiny Blob / Logic App /
environment costs may remain. Use `azure-pipelines-control.yml` to start manually.

### First deployment checklist

1. Create resource group and register providers
2. Configure Azure DevOps variables (Entra + GHCR)
3. Run pipeline on `main` (infra deploy; smoke may fail)
4. Run `docs/azure/sql-grants.sql` for `managedIdentityName`
5. Re-run pipeline on `main`
6. Deploy `cost-guard.bicep` with Owner account
7. Register `azure-pipelines-control.yml` (uses environment `test` for start)

Remove legacy App Service resources manually after ACA is validated; SQL and Blob
data are preserved.
