# Azure Pipelines — test deploy (E4)

One-time setup in Azure DevOps so `azure-pipelines.yml` can deploy the **test**
environment after a successful build on `main`.

## Pipeline behaviour

| Trigger | Build + tests | Deploy test |
|---------|---------------|-------------|
| PR → `main` | Yes | No |
| Push → `main` | Yes | Yes |

Deploy stage steps:

1. Create/update resource group (`infra/subscription.bicep`)
2. Deploy App Service, SQL, storage (`infra/main.bicep`)
3. Zip-deploy the published Web app
4. HTTP smoke check on the default hostname

No passwords or connection strings are stored in the repo. Entra IDs and the
Azure service connection are configured in Azure DevOps only.

## 1) Azure service connection

In **Project settings → Service connections**, create an **Azure Resource
Manager** connection (workload identity federation or service principal) with
rights to deploy resources in your subscription, for example:

- Contributor on resource group `rg-recipelibrary-test-weu`, or
- Contributor on the subscription (simpler for test)

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

The pipeline does not run T-SQL. After the **first** successful infra deploy,
grant the Web App managed identity access to the database:

1. Open the Web App → **Identity** → note the managed identity name (same as
   the web app name).
2. Connect to Azure SQL with Entra ID (Azure Data Studio).
3. Run `docs/azure/sql-grants.sql` for that principal.

Subsequent deploys only update the app package; grants persist.

See also `docs/azure/test-runbook.md` for manual laptop deploy and local debug.

## 5) Verify

1. Merge to `main` (or run the pipeline on `main`).
2. Open the **Deploy test environment** stage logs.
3. Confirm the smoke check step prints a `200` status and the test URL.

## Troubleshooting

- **Service connection not authorized**: approve the connection when prompted.
- **Empty `AZURE_*` variables**: deploy fails at Bicep; set secrets in pipeline
  settings.
- **App starts but DB errors**: run SQL grants for the managed identity.
- **Free SQL paused**: wake the database from the portal or wait for auto-resume.
