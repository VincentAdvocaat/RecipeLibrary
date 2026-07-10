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
**Contributor on resource group** `rg-recipelibrary-test-weu`.

Contributor alone cannot create `Microsoft.Authorization/roleAssignments`
(Bicep grants the Web App managed identity **Storage Blob Data Contributor** on
the storage account). Grant the service principal **Role Based Access Control
Administrator** on the same resource group, with a condition that allows only
that role (one-time, outside the pipeline):

```bash
az role assignment create \
  --assignee-object-id <service-principal-object-id> \
  --assignee-principal-type ServicePrincipal \
  --role "Role Based Access Control Administrator" \
  --scope "/subscriptions/<subscription-id>/resourceGroups/rg-recipelibrary-test-weu" \
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

These map to Bicep parameters for the SQL server Entra admin. They are **not**
checked into git.

## 3) Deployment environment

Create **Pipelines → Environments → test** (name must match `environment: test`
in `azure-pipelines.yml`).

Optional: add an approval check on `test` so deploys to Azure require manual
confirmation.

## 4) One-time SQL grants (after first infra deploy)

The pipeline does not run T-SQL. Blob RBAC for recipe images is created by
`infra/main.bicep` during deploy (idempotent). After the **first** successful
infra deploy, grant the Web App managed identity access to the database:

1. Open the Web App → **Identity** → note the managed identity name (same as
   the web app name).
2. Connect to Azure SQL with Entra ID (Azure Data Studio).
3. Run `docs/azure/sql-grants.sql` for that principal.

Subsequent pipeline deploys only update the app package; SQL grants persist.

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
- **InvalidTemplateDeployment / roleAssignments/write**: grant the pipeline
  service principal **Role Based Access Control Administrator** on the resource
  group with the Storage Blob Data Contributor condition (see step 1). Do not use
  **User Access Administrator** unless you accept broader role-assignment rights.
- **Blob upload errors**: confirm the Web App managed identity has **Storage Blob
  Data Contributor** on the storage account (created by Bicep on deploy).
