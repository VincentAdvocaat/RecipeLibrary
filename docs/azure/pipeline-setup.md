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
az provider register --namespace Microsoft.ManagedIdentity
az provider register --namespace Microsoft.OperationalInsights
```

Registering extra providers (for example `Microsoft.DBforPostgreSQL` or
`Microsoft.DBforMySQL`) is **harmless**: it only enables those services on the
subscription. You are **not billed** until you actually create resources. Safe to
leave registered.

Or use `infra/subscription.bicep` from your laptop (`az deployment sub create`).
See also `docs/azure/subscription-bootstrap.md`.

The pipeline only deploys `infra/main.bicep` at resource-group scope.

## 1) Azure service connections (one per region)

Use **separate, clearly named** service connections so you can deploy to North
Europe or Sweden Central independently. Both can target the same subscription.

| ADO connection name | Resource group | Region | Used by |
|---------------------|----------------|--------|---------|
| `RecipeLibrary-Azure-SEC` | `rg-recipelibrary-test-sec` | `swedencentral` | Main + control pipelines (default) |
| `RecipeLibrary-Azure-NEU` | `rg-recipelibrary-test-neu` | `northeurope` | Manual / fallback if NEU quota opens |

### Rename the existing connection (NEU)

1. **Project settings → Service connections**
2. Open the current connection (for example `RecipeLibrary-Azure`)
3. **Edit** → rename to **`RecipeLibrary-Azure-NEU`**
4. Confirm scope still includes `rg-recipelibrary-test-neu` (or subscription)

Keep this connection even when the main pipeline uses SEC — it preserves the
option to deploy to NEU later.

### Create the SEC connection

1. **New service connection → Azure Resource Manager**
2. **Workload identity federation (automatic)** (recommended)
3. Subscription: **Draconis-labs subscription**
4. Resource group scope: **`rg-recipelibrary-test-sec`**
5. Name: **`RecipeLibrary-Azure-SEC`**
6. Grant access to all pipelines (or authorize on first run)

Note the new service principal **object id** from the connection details page.

### RBAC per resource group (Owner account, one-time per SP + RG)

Each pipeline service principal needs on **its** resource group:

- **Contributor**
- **Role Based Access Control Administrator** with a condition that allows only
  **Storage Blob Data Contributor** assignments (for Bicep blob RBAC)

Example for SEC (replace `<sp-object-id>`):

```bash
az role assignment create \
  --assignee-object-id <sp-object-id> \
  --assignee-principal-type ServicePrincipal \
  --role "Contributor" \
  --scope "/subscriptions/<subscription-id>/resourceGroups/rg-recipelibrary-test-sec"

az role assignment create \
  --assignee-object-id <sp-object-id> \
  --assignee-principal-type ServicePrincipal \
  --role "Role Based Access Control Administrator" \
  --scope "/subscriptions/<subscription-id>/resourceGroups/rg-recipelibrary-test-sec" \
  --condition "((!(ActionMatches{'Microsoft.Authorization/roleAssignments/write'})) OR (@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {ba92f5b4-2d11-453d-a403-e96b0029c9fe})) AND ((!(ActionMatches{'Microsoft.Authorization/roleAssignments/delete'})) OR (@Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {ba92f5b4-2d11-453d-a403-e96b0029c9fe}))" \
  --condition-version "2.0"
```

Repeat with `rg-recipelibrary-test-neu` for the NEU service principal.

You can use **one shared service principal** for both regions (grant both RGs)
or **separate SPs** per connection — separate SPs are clearer for isolation.

The pipeline variable `azureServiceConnection` selects which connection runs
(default: `RecipeLibrary-Azure-SEC`). To deploy to NEU manually, override
`azureServiceConnection`, `azureLocation`, and `resourceGroupName` when queuing
a run.

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

## 3) Deployment environments

Create **Pipelines → Environments**:

| Environment | Used by | Purpose |
|-------------|---------|---------|
| `test-sec` | `azure-pipelines.yml` SEC deploy; control pipeline hibernate for SEC | Sweden Central test |
| `test-neu` | `azure-pipelines.yml` NEU deploy; control pipeline hibernate for NEU | North Europe test (optional) |

Optional: add an approval check on these environments so deploys or hibernate
runs require confirmation.

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

## 6) Cost control pipeline

Register **`azure-pipelines-control.yml`** as a separate pipeline with
**no CI trigger**.

**Usage**

| Goal | What to run |
|------|-------------|
| Costs nearly off (keep data) | Control pipeline → pick region → Run (default) |
| Inspect only | Control pipeline → enable **Status only** → Run |
| App back on | Main pipeline (`azure-pipelines.yml`) for that region |

Default run **hibernates**: deletes Container App + Managed Environment, pauses SQL.
SQL databases, Blob storage, and the managed identity stay. Residual costs are
mainly storage (free-offer limits still apply).

Hibernate uses environments `test-sec` / `test-neu`. If you use `cost-guard.bicep`,
redeploy it once after the Container App exists again (RBAC on the app is removed
with hibernate).

| Parameter | Values | Default |
|-----------|--------|---------|
| `target` | `sec`, `neu`, `all` | `sec` |
| `statusOnly` | checkbox | off (hibernate) |

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
