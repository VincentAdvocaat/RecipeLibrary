## Azure DevOps – status

De GitHub ↔ Azure DevOps integratie is actief. Zie **`ado-github-integration.md`**
voor de dagelijkse workflow (clone vanaf GitHub, `AB#` in commits, pipeline-links).

De pipeline draait vanaf `azure-pipelines.yml` in GitHub met stages **Build** en
**DeployTest**. Eenmalige ADO-setup: `pipeline-setup.md`. Onderstaand schema
beschrijft dezelfde stappen voor referentie.

### Minimal pipeline sketch

This is intentionally minimal and cost-conscious. Azure DevOps has a free tier for small teams, but build minutes/agents depend on your setup. The simplest path is:

### Stages

1) **Infra**
- Install Azure CLI + Bicep
- `az deployment sub create ...` (RG)
- `az deployment group create ...` (App Service + SQL)

2) **Build**
- `dotnet restore`
- `dotnet build -c Release`
- `dotnet publish ... -o $(Build.ArtifactStagingDirectory)/publish`
- Publish artifact

3) **Deploy**
- Download artifact
- `az webapp deploy --type zip --src-path ...`

### Secrets policy

- Do **not** store passwords.
- Use Entra/Managed Identity.
- Only store non-secret config (like server/db names) as pipeline variables if needed.

