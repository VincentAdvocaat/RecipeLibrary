# OpenAI API key in Azure (Key Vault)

Recipe import AI is **on by default** in Azure. The OpenAI API key lives in
**Key Vault**; the Container App reads it via a Key Vault secret reference and
managed identity. The key is **never** stored in the repo or as a plain
Container App secret value.

## Resources (from `infra/main.bicep`)

| Resource | Purpose |
|----------|---------|
| Key Vault (RBAC, soft-delete, purge protection) | Stores `RecipeImport-OpenAi-ApiKey` |
| App managed identity | **Key Vault Secrets User** (read at runtime) |
| Bootstrap script identity | **Key Vault Secrets Officer** (creates placeholder once if missing) |
| Entra SQL admin user | **Key Vault Secrets Officer** (set/rotate via CLI) |
| Container App env | `RecipeImport__Ai__Enabled=true` + `RecipeImport__Ai__ApiKey` from Key Vault |

First deploy creates a placeholder secret value `UNSET` if the secret does not
exist yet. Later infrastructure deploys **do not overwrite** a secret you set
via CLI.

## One-time setup after first infra deploy

1. Widen the pipeline SP **Role Based Access Control Administrator** condition so
   it may assign Key Vault Secrets User/Officer (see `docs/azure/pipeline-setup.md`).
2. Register `Microsoft.KeyVault` if needed.
3. Deploy once (Key Vault + AI wiring).
4. Set your real OpenAI key with the CLI below and restart the Container App.

Hibernate deletes the Container App but **keeps Key Vault**; after wake-up the
secret reference is wired again automatically.

## Azure CLI — set or rotate the OpenAI secret (no redeploy)

```bash
RG=rg-recipelibrary-test-sec
KV=$(az keyvault list -g "$RG" --query "[0].name" -o tsv)
CA=$(az containerapp list -g "$RG" --query "[0].name" -o tsv)
echo "Key Vault: $KV"
echo "Container App: $CA"

# Set or rotate the key (creates a new secret version; does not require Bicep deploy)
az keyvault secret set \
  --vault-name "$KV" \
  --name "RecipeImport-OpenAi-ApiKey" \
  --value "sk-YOUR-OPENAI-KEY"

# Restart so the app picks up the latest Key Vault secret version
REV=$(az containerapp revision list -g "$RG" -n "$CA" --query "[0].name" -o tsv)
az containerapp revision restart -g "$RG" -n "$CA" --revision "$REV"
```

PowerShell:

```powershell
$RG = "rg-recipelibrary-test-sec"
$KV = (az keyvault list -g $RG --query "[0].name" -o tsv)
$CA = (az containerapp list -g $RG --query "[0].name" -o tsv)

az keyvault secret set `
  --vault-name $KV `
  --name "RecipeImport-OpenAi-ApiKey" `
  --value "sk-YOUR-OPENAI-KEY"

$REV = (az containerapp revision list -g $RG -n $CA --query "[0].name" -o tsv)
az containerapp revision restart -g $RG -n $CA --revision $REV
```

## Local development

Use `.env` (gitignored) or user secrets — see `.env.example`. Do not put the key
in `appsettings*.json`.
