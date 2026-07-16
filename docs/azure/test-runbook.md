## Azure test environment runbook (Container Apps Consumption + Azure SQL Free Offer)

### Goals

- Deploy **test** infra in `swedencentral` using Bicep
- Run the Blazor Server app on **Azure Container Apps Consumption** (scale-to-zero, max 1 replica)
- Pull an immutable **public GHCR** image (`ghcr.io/<owner>/recipelibrary@sha256:...`)
- Use **Azure SQL Database Free Offer** with:
  - `useFreeLimit: true`
  - `freeLimitExhaustionBehavior: AutoPause` (no charges; DB pauses until next month when the free limit is exhausted)
- Use **password-less** auth everywhere:
  - **Local**: Entra ID (Azure Data Studio / `dotnet run`)
  - **Azure**: user-assigned managed identity on the Container App

### Prerequisites (laptop)

- .NET SDK (your repo targets `net10.0`)
- Azure CLI + Bicep:
  - Install Azure CLI
  - `az bicep install` (or `az bicep upgrade`)
- Docker (for local Release image validation)
- Azure Data Studio (optional, recommended for querying)

Register providers once per subscription (see `docs/azure/subscription-bootstrap.md`):

```bash
az provider register --namespace Microsoft.App
az provider register --namespace Microsoft.Sql
az provider register --namespace Microsoft.Storage
```

### 1) Gather required Entra inputs for Bicep

From a terminal:

- `az login`
- `az account show --query tenantId -o tsv` → use for `tenantId`
- `az ad signed-in-user show --query id -o tsv` → use for `entraAdminObjectId`
- Your UPN (e.g. `you@domain`) → use for `entraAdminLogin`

Optional (for laptop SQL firewall):

- Find your public IP and set it as `clientPublicIp`

### 2) Deploy infrastructure (Bicep)

From repo root:

1. Create RG (subscription scope):
   - `az deployment sub create --location swedencentral --template-file infra/subscription.bicep --parameters projectName=recipelibrary environment=test`

2. Build and push a GHCR image (or use the pipeline on `main`), then note the digest.

3. Deploy resources into RG (resource-group scope):
   - Update `infra/params/test.bicepparam` with your real tenant/user values and `containerImageDigest`
   - `az deployment group create -g rg-recipelibrary-test-sec --template-file infra/main.bicep --parameters infra/params/test.bicepparam`

Record outputs:

- `containerAppName`
- `containerAppFqdn`
- `managedIdentityName` (for SQL grants)
- `sqlServerFqdn`
- `sqlDatabaseName`

### 3) Grant Azure SQL permissions (Entra + Managed Identity)

Open Azure Data Studio and connect with Entra ID:

- Server: `<sqlServerFqdn>`
- Database: `<sqlDatabaseName>`
- Authentication: Azure Active Directory (Entra)

Run the script in `docs/azure/sql-grants.sql`:

- once for your own user (optional if you’re Entra admin + have rights)
- once for the **user-assigned managed identity** display name (`managedIdentityName` output)

Blob access for recipe images and Data Protection keys is granted by
`infra/main.bicep` (Storage Blob Data Contributor on the storage account for the
managed identity). Redeploying the template is idempotent.

### 4) Two-stage first deployment (pipeline)

When using `azure-pipelines.yml`:

1. **First run on `main`**: Bicep + Container App deploy succeeds; smoke check may fail until SQL grants exist.
2. Apply SQL grants (step 3) for `managedIdentityName`.
3. **Second run on `main`**: new revision becomes healthy; smoke check should pass.

Cold starts (scale-from-zero + serverless SQL resume) can take several minutes.
The pipeline smoke check retries for up to ~10 minutes.

### 5) Deploy via pipeline (preferred)

After one-time setup in `docs/azure/pipeline-setup.md`, merges to `main` run the
**Deploy test environment** stage automatically (Bicep + immutable GHCR digest).
Use laptop deploy below only for ad-hoc debugging.

### 6) Deploy the app from your laptop (ad-hoc)

1. Build Release image:
   - `docker build -f src/Web/RecipeLibrary.Web/Dockerfile -t ghcr.io/<owner>/recipelibrary:local .`

2. Push to GHCR and resolve digest, then redeploy Bicep with `containerImageDigest`.

### 7) Local dev against Azure SQL (no secrets)

Set a password-less connection string for your local run:

- `ConnectionStrings__RecipeDb=Server=tcp:<sqlServerFqdn>,1433;Database=<sqlDatabaseName>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Default;`

Then:

- `az login` (if not already)
- `dotnet run --project src/Web/RecipeLibrary.Web/RecipeLibrary.Web.csproj`

Notes:

- The app runs migrations on startup (`Database.Migrate()`), so your user needs DDL rights during dev/test.
- Disconnect query tools when done, otherwise the serverless DB may not auto-pause and will burn free vCore seconds faster.

### 8) Cost guard and cost control pipeline

- Deploy `infra/cost-guard.bicep` once with an Owner account (see `infra/README.md`).
- Use `azure-pipelines-control.yml` to **hibernate** (default) or **status only**.
  Hibernate uses environments `test-sec` / `test-neu`.

| Goal | What to run |
|------|-------------|
| Costs nearly off, keep data | Control pipeline (default hibernate) |
| App back on | Main deploy pipeline (`azure-pipelines.yml`) |

Cost ingestion is delayed; the €5 budget is an alert threshold, not a hard cap.
After hibernate, SQL is paused and Blob data remains; tiny storage costs may remain.

### 9) Verify scale-to-zero and auth cookies

1. Wait for idle period (scale-to-zero) or hibernate via the control pipeline, then redeploy.
2. Browse the FQDN — expect a cold start delay.
3. Sign in (when Entra is configured) and confirm the session survives a second cold start (Data Protection keys in Blob Storage).
