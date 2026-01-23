## Azure test environment runbook (App Service Free + Azure SQL Free Offer)

### Goals

- Deploy **test** infra in `westeurope` using Bicep
- Run the Blazor Server app on **App Service F1**
- Use **Azure SQL Database Free Offer** with:
  - `useFreeLimit: true`
  - `freeLimitExhaustionBehavior: AutoPause` (no charges; DB pauses until next month when free limit is exhausted)
- Use **password-less** auth everywhere:
  - **Local**: Entra ID (Azure Data Studio / `dotnet run`)
  - **Azure**: Web App System Assigned Managed Identity

### Prerequisites (laptop)

- .NET SDK (your repo targets `net10.0`)
- Azure CLI + Bicep:
  - Install Azure CLI
  - `az bicep install` (or `az bicep upgrade`)
- Azure Data Studio (optional, recommended for querying)

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
   - `az deployment sub create --location westeurope --template-file infra/subscription.bicep --parameters projectName=recipelibrary environment=test`

2. Deploy resources into RG (resource-group scope):
   - Update `infra/params/test.bicepparam` with your real tenant/user values
   - `az deployment group create -g rg-recipelibrary-test-weu --template-file infra/main.bicep --parameters infra/params/test.bicepparam`

Record outputs:
- `webAppName`
- `sqlServerFqdn`
- `sqlDatabaseName`
- `webAppManagedIdentityPrincipalId`

### 3) Grant Azure SQL permissions (Entra + Managed Identity)

Open Azure Data Studio and connect with Entra ID:

- Server: `<sqlServerFqdn>`
- Database: `<sqlDatabaseName>`
- Authentication: Azure Active Directory (Entra)

Run the script in `docs/azure/sql-grants.sql`:
- once for your own user (optional if you’re Entra admin + have rights)
- once for the Web App Managed Identity display name (see portal -> Web App -> Identity)

### 4) Deploy the app from your laptop

1. Publish:
   - `dotnet publish src/Web/RecipeLibrary.Web/RecipeLibrary.Web.csproj -c Release -o ./.publish`

2. Zip deploy:
   - `az webapp deploy --resource-group rg-recipelibrary-test-weu --name <webAppName> --src-path ./.publish --type zip`

### 5) Local dev against Azure SQL (no secrets)

Set a password-less connection string for your local run:

- `ConnectionStrings__RecipeDb=Server=tcp:<sqlServerFqdn>,1433;Database=<sqlDatabaseName>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Default;`

Then:
- `az login` (if not already)
- `dotnet run --project src/Web/RecipeLibrary.Web/RecipeLibrary.Web.csproj`

Notes:
- The app runs migrations on startup (`Database.Migrate()`), so your user needs DDL rights during dev/test.
- Disconnect query tools when done, otherwise the serverless DB may not auto-pause and will burn free vCore seconds faster.

