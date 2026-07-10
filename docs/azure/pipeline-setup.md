# Azure Pipelines — test deploy (E4)

One-time setup in Azure DevOps so `azure-pipelines.yml` can deploy the **test**
environment after a successful build on `main`.

## Pipeline behaviour

| Trigger | Build + tests | Deploy test |
|---------|---------------|-------------|
| PR → `main` | Yes | No |
| Push → `main` | Yes | Yes |

Deploy stage steps:

1. Verify the target resource group already exists (pipeline does **not** deploy at subscription scope)
2. Deploy App Service, SQL, storage (`infra/main.bicep`) into that group
3. Zip-deploy the published Web app
4. HTTP smoke check on the default hostname

No passwords or connection strings are stored in the repo. Entra IDs and the
Azure service connection are configured in Azure DevOps only.

## 0) Resource group (manual, one-time)

Create the resource group **before** the first pipeline deploy, for example:

```bash
az group create --name rg-recipelibrary-test-weu --location westeurope
```

Or use `infra/subscription.bicep` from your laptop (`az deployment sub create`).
The pipeline only deploys `infra/main.bicep` at resource-group scope.

## 1) Azure service connection

In **Project settings → Service connections**, create an **Azure Resource
Manager** connection (workload identity federation or service principal) with
**Contributor on resource group** `rg-recipelibrary-test-weu` only.

Contributor is enough for Bicep deploy. Role assignments (blob access for the
Web App managed identity) are **not** in Bicep — run once after the first deploy
(see step 4).

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

These map to Bicep parameters for the SQL server Entra admin. They are **not**
checked into git.

## 3) Deployment environment

Create **Pipelines → Environments → test** (name must match `environment: test`
in `azure-pipelines.yml`).

Optional: add an approval check on `test` so deploys to Azure require manual
confirmation.

## 4) One-time grants (after first infra deploy)

The pipeline does not run T-SQL or create Azure RBAC role assignments (the
deploy service principal typically has Contributor only).

### SQL (managed identity → database)

1. Open the Web App → **Identity** → note the managed identity name (same as
   the web app name).
2. Connect to Azure SQL with Entra ID (Azure Data Studio).
3. Run `docs/azure/sql-grants.sql` for that principal.

### Blob storage (managed identity → storage account)

From a shell with **User Access Administrator** or **Owner** on the resource
group (your user account is fine):

```bash
./docs/azure/storage-blob-rbac.sh rg-recipelibrary-test-weu <webAppName>
```

`<webAppName>` is in the Bicep deploy output (`webAppName`) or the App Service
name in the portal.

Subsequent pipeline deploys only update the app package; grants persist.

See also `docs/azure/test-runbook.md` for manual laptop deploy and local debug.

## 5) Verify

1. Merge to `main` (or run the pipeline on `main`).
2. Open the **Deploy test environment** stage logs.
3. Confirm the smoke check step prints a `200` status and the test URL.

## Troubleshooting

- **Resource group not found**: create `rg-recipelibrary-test-weu` manually; the
  pipeline does not provision resource groups or subscription-scoped resources.
- **Service connection not authorized**: approve the connection when prompted.
- **Empty `AZURE_*` variables**: deploy fails at Bicep; set secrets in pipeline
  settings.
- **App starts but DB errors**: run SQL grants for the managed identity.
- **InvalidTemplateDeployment / roleAssignments/write**: Bicep no longer creates
  blob RBAC; run `docs/azure/storage-blob-rbac.sh` once with an account that can
  assign roles. Alternatively grant the pipeline service principal **User Access
  Administrator** on the resource group and restore role assignment in Bicep.
