# OpenAI API key in Azure (Key Vault)

Recipe import AI uses an OpenAI API key. In Azure the key lives in **Key Vault**;
the Container App reads it via a Key Vault secret reference and managed identity.
The key is **never** stored in the repo or as a plain Container App secret value.

## Resources (from `infra/main.bicep`)

| Resource | Purpose |
|----------|---------|
| Key Vault (RBAC, soft-delete, purge protection) | Stores `RecipeImport-OpenAi-ApiKey` |
| App managed identity | **Key Vault Secrets User** (read at runtime) |
| Entra SQL admin user | **Key Vault Secrets Officer** (set/rotate via CLI) |

## Deploy order

0. One-time: register `Microsoft.KeyVault` and widen the pipeline SP **Role Based
   Access Control Administrator** condition so it may assign Key Vault Secrets
   User/Officer (see `docs/azure/pipeline-setup.md`).
1. Deploy `main.bicep` with `enableRecipeImportAi=false` (default). Key Vault is created.
2. Set the secret with Azure CLI (below).
3. Redeploy with `enableRecipeImportAi=true` (pipeline variable or Bicep param) so the
   Container App gets `RecipeImport__Ai__Enabled` and `RecipeImport__Ai__ApiKey`.

Hibernate deletes the Container App but **keeps Key Vault**; after wake-up, keep
`enableRecipeImportAi=true` so the secret reference is wired again.

## Pipeline variable

Optional Azure DevOps variable (library or pipeline):

| Name | Secret? | Example |
|------|---------|---------|
| `ENABLE_RECIPE_IMPORT_AI` | No | `true` after the Key Vault secret exists |

## Azure CLI — set the OpenAI secret

Replace resource group / names with deployment outputs (`keyVaultName`,
`openAiKeyVaultSecretName`, `containerAppName`).

```bash
# 1) Resolve Key Vault name from the last deploy (or copy from portal)
RG=rg-recipelibrary-test-sec
KV=$(az keyvault list -g "$RG" --query "[0].name" -o tsv)
echo "Key Vault: $KV"

# 2) Store the OpenAI API key (you will be prompted if you omit the value flag)
az keyvault secret set \
  --vault-name "$KV" \
  --name "RecipeImport-OpenAi-ApiKey" \
  --value "sk-YOUR-OPENAI-KEY"

# 3) Confirm (shows metadata only, not the secret value by default with show)
az keyvault secret show \
  --vault-name "$KV" \
  --name "RecipeImport-OpenAi-ApiKey" \
  --query "{name:name,enabled:attributes.enabled,updated:attributes.updated}" -o table
```

PowerShell:

```powershell
$RG = "rg-recipelibrary-test-sec"
$KV = (az keyvault list -g $RG --query "[0].name" -o tsv)
az keyvault secret set `
  --vault-name $KV `
  --name "RecipeImport-OpenAi-ApiKey" `
  --value "sk-YOUR-OPENAI-KEY"
```

Then enable AI on the next infra deploy (`enableRecipeImportAi=true` /
`ENABLE_RECIPE_IMPORT_AI=true`). Until that flag is on, the app runs without AI
(same as local with `Enabled=false`).

## Rotate the key

```bash
az keyvault secret set \
  --vault-name "$KV" \
  --name "RecipeImport-OpenAi-ApiKey" \
  --value "sk-NEW-KEY"

# Restart Container App so it picks up the new secret version
CA=$(az containerapp list -g "$RG" --query "[0].name" -o tsv)
az containerapp revision restart -g "$RG" -n "$CA" --revision $(az containerapp revision list -g "$RG" -n "$CA" --query "[0].name" -o tsv)
```

## Local development

Use `.env` (gitignored) or user secrets — see `.env.example`. Do not put the key
in `appsettings*.json`.
