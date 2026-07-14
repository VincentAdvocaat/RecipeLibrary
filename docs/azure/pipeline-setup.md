# Azure Pipelines — test deploy (Container Apps)

One-time setup in Azure DevOps so `azure-pipelines.yml` can deploy the **test**
environment after a successful build on `main`.

## Pipeline behaviour

| Trigger | Build + tests | Build container | Push GHCR | Deploy test |
|---------|---------------|-----------------|-----------|-------------|
| PR → `main` | Yes | Yes (validation) | No | No |
| Push → `main` | Yes | Yes | Yes | Yes |

Deploy stage steps:

1. Verify the target resource group already exists (pipeline does **not** deploy at subscription scope)
2. Deploy Container Apps, SQL, storage (`infra/main.bicep`) with an **immutable GHCR digest**
3. Extended HTTP smoke check (`/health` and `/`) tolerant of scale-to-zero cold starts

No passwords or connection strings are stored in the repo. Entra IDs, GHCR
credentials, and the Azure service connection are configured in Azure DevOps only.

## 0) Resource group and providers (manual, one-time)

Create the resource group **before** the first pipeline deploy, for example:

```bash
az group create --name rg-recipelibrary-test-sec --location swedencentral
```

Register required resource providers (once per subscription):

```bash
az provider register --namespace Microsoft.App
az provider register --namespace Microsoft.Sql
az provider register --namespace Microsoft.Storage
az provider register --namespace Microsoft.OperationalInsights
```

Or use `infra/subscription.bicep` from your laptop (`az deployment sub create`).
See also `docs/azure/subscription-bootstrap.md`.

The pipeline only deploys `infra/main.bicep` at resource-group scope.

## 1) Azure service connection

In **Project settings → Service connections**, create an **Azure Resource
Manager** connection (workload identity federation or service principal) with
**Contributor on resource group** `rg-recipelibrary-test-sec`.

Contributor alone cannot create `Microsoft.Authorization/roleAssignments`
(Bicep grants the Container App user-assigned identity **Storage Blob Data
Contributor** on the storage account). Grant the service principal **Role Based
Access Control Administrator** on the same resource group, with a condition that
allows only that role (one-time, outside the pipeline):

```bash
az role assignment create \
  --assignee-object-id <service-principal-object-id> \
  --assignee-principal-type ServicePrincipal \
  --role "Role Based Access Control Administrator" \
  --scope "/subscriptions/<subscription-id>/resourceGroups/rg-recipelibrary-test-sec" \
  --condition "((!(ActionMatches{'Microsoft.Authorization/roleAssignments/write'})) OR (@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {ba92f5b4-2d11-453d-a403-e96b0029c9fe})) AND ((!(ActionMatches{'Microsoft.Authorization/roleAssignments/delete'})) OR (@Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {ba92f5b4-2d11-453d-a403-e96b0029c9fe}))" \
  --condition-version "2.0"
```

Use the service principal **object id** from the service connection (not the
application/client id). The Bicep role assignment uses a stable `guid()` name and
is idempotent on redeploy.

Name the connection **`RecipeLibrary-Azure`** (or override pipeline variable
`azureServiceConnection`).

Grant the connection access to all pipelines (or authorize when the first
deploy runs).

## 2) Pipeline variables (secrets)

In **Pipelines → VincentAdvocaat.RecipeLibrary → Edit → Variables**, add these
as **secret** variables (or use a variable group `RecipeLibrary-Test` and link
it to the pipeline):

| Variable | Example source |
|----------|----------------|
| `AZURE_TENANT_ID` | `az account show --query tenantId -o tsv` |
| `AZURE_ENTRA_ADMIN_LOGIN` | Your UPN |
| `AZURE_ENTRA_ADMIN_OBJECT_ID` | `az ad signed-in-user show --query id -o tsv` |
| `GHCR_USERNAME` | GitHub username or `github-actions` bot user |
| `GHCR_TOKEN` | GitHub PAT with `write:packages` (classic) or fine-grained packages write |

These map to Bicep parameters and GHCR login. They are **not** checked into git.

The public image repository defaults to `ghcr.io/vincentadvocaat/recipelibrary`
(pipeline variable `ghcrImageRepository`). After the first push, keep the package
**public** so Container Apps can pull anonymously (no registry secret at runtime).

## 3) Deployment environment

Create **Pipelines → Environments**:

| Environment | Used by | Purpose |
|-------------|---------|---------|
| `test` | `azure-pipelines.yml` deploy stage; `azure-pipelines-control.yml` start action | Test deploy and manual start after stop |

Optional: add an approval check on `test` so deploys or manual starts require
confirmation.

## 4) Two-stage first deployment and SQL grants

The pipeline does not run T-SQL. Blob RBAC for recipe images and Data Protection
keys is created by `infra/main.bicep` during deploy (idempotent).

**Stage 1 — infra + first revision (expect unhealthy until SQL grant):**

1. Merge to `main` (or run the pipeline on `main`).
2. Confirm Bicep deploy succeeds and note outputs:
   - `containerAppName`
   - `managedIdentityName` (user-assigned identity name for SQL grants)
3. The smoke check may **fail** until database permissions exist — that is expected.

**Stage 2 — SQL grants + redeploy:**

1. Connect to Azure SQL with Entra ID (Azure Data Studio).
2. Run `docs/azure/sql-grants.sql` using the **managed identity name** from step 2
   (not the Container App name).
3. Re-run the pipeline on `main` (or restart the Container App revision).

Subsequent pipeline deploys update the immutable image digest; SQL grants persist.

See also `docs/azure/test-runbook.md` for manual laptop deploy and local debug.

## 5) Cost guard (separate deploy)

Deploy `infra/cost-guard.bicep` **once** with an Owner account (not the
constrained pipeline service principal). This provisions a €5 monthly budget,
email warnings, and a Logic App that stops the Container App at 80% actual spend.

See `infra/README.md` for parameters and operational notes.

## 6) Emergency control pipeline

Register **`azure-pipelines-control.yml`** as a separate pipeline with
**no CI trigger**. Default action is `status`; `start` uses the same `test`
environment as the main deploy pipeline.

## 7) Verify

1. Merge to `main` (after SQL grants).
2. Open the **Deploy test environment** stage logs.
3. Confirm the smoke check prints HTTP `200` for `/health` and `/`.

## Troubleshooting

- **Resource group not found**: create `rg-recipelibrary-test-sec` manually; the
  pipeline does not provision resource groups or subscription-scoped resources.
- **Provider not registered**: run `az provider register --namespace Microsoft.App`.
- **Service connection not authorized**: approve the connection when prompted.
- **Empty `AZURE_*` or GHCR variables**: deploy fails at Bicep or docker login; set
  secrets in pipeline settings.
- **App starts but DB errors**: run SQL grants for the user-assigned managed identity.
- **Smoke check timeout on first deploy**: apply SQL grants and redeploy.
- **InvalidTemplateDeployment / roleAssignments/write**: grant the pipeline
  service principal **Role Based Access Control Administrator** on the resource
  group with the Storage Blob Data Contributor condition (see step 1).
- **Blob upload or auth cookie errors**: confirm the managed identity has **Storage
  Blob Data Contributor** on the storage account (created by Bicep on deploy).
- **RequestDisallowedByAzure / locationineligible**: create the resource group in
  an eligible region (this repo uses `swedencentral`).
