# YouTube Data API key in Azure (Key Vault)

YouTube Shorts / video URL import prefers the **YouTube Data API v3**
(`videos.list` → `snippet.description`) so captions work from Azure datacenter
IPs. InnerTube remains a local/dev fallback when no key is configured. The API
key lives in **Key Vault**; the Container App reads it via a Key Vault secret
reference and managed identity. The key is **never** stored in the repo or as a
plain Container App secret value.

## Resources (from `infra/main.bicep`)

| Resource | Purpose |
|----------|---------|
| Key Vault (RBAC, soft-delete, purge protection) | Stores `RecipeImport-YouTube-ApiKey` |
| App managed identity | **Key Vault Secrets User** (read at runtime) |
| Bootstrap script identity | **Key Vault Secrets Officer** (creates placeholder once if missing) |
| Entra SQL admin user | **Key Vault Secrets Officer** (set/rotate via CLI) |
| Container App env | `RecipeImport__YouTube__ApiKey` from Key Vault |

First deploy creates a placeholder secret value `UNSET` if the secret does not
exist yet. Later infrastructure deploys **do not overwrite** a secret you set
via CLI. The app treats `UNSET` as “no key” and falls back to InnerTube.

Hibernate deletes the Container App but **keeps Key Vault**; after wake-up the
secret reference is wired again automatically.

## Cost / quota

The YouTube Data API v3 is **free**. There is no paid tier. Default quota is
about **10,000 units/day** per Google Cloud project; `videos.list` costs **1
unit** per call (one Shorts import ≈ 1 unit).

---

## A. YouTube Data API-key aanvragen (Google Cloud)

1. Ga naar [Google Cloud Console](https://console.cloud.google.com/).
2. Maak een project (of kies bestaand), bijv. `RecipeLibrary`.
3. **APIs & Services → Library** → zoek **YouTube Data API v3** → **Enable**.
4. **APIs & Services → Credentials** → **Create credentials** → **API key**.
5. Klik de nieuwe key → **Edit API key**:
   - **API restrictions**: Restrict key → alleen **YouTube Data API v3**.
   - **Application restrictions**: voor server-side bij voorkeur **None**
     (of IP allowlist als je vaste egress-IP hebt; Container Apps hebben die
     meestal niet).
6. **Save** en kopieer de key één keer veilig (niet in git/chatlogs).

---

## B. Key in Azure Key Vault zetten (CLI)

Na minstens één infra-deploy (zodat Key Vault + placeholder bestaan).

### Bash

```bash
RG=rg-recipelibrary-test-sec
KV=$(az keyvault list -g "$RG" --query "[0].name" -o tsv)
CA=$(az containerapp list -g "$RG" --query "[0].name" -o tsv)
echo "Key Vault: $KV"
echo "Container App: $CA"

# Set or rotate the key via stdin (avoids putting the key in shell history).
read -r -s -p "YouTube Data API key: " YT_KEY; echo
printf '%s' "$YT_KEY" | az keyvault secret set \
  --vault-name "$KV" \
  --name "RecipeImport-YouTube-ApiKey" \
  --file /dev/stdin \
  --encoding utf-8 >/dev/null
unset YT_KEY

# Restart so the app picks up the latest Key Vault secret version
REV=$(az containerapp revision list -g "$RG" -n "$CA" --query "[0].name" -o tsv)
az containerapp revision restart -g "$RG" -n "$CA" --revision "$REV"
```

### PowerShell

```powershell
$RG = "rg-recipelibrary-test-sec"
$KV = (az keyvault list -g $RG --query "[0].name" -o tsv)
$CA = (az containerapp list -g $RG --query "[0].name" -o tsv)

# Prompt securely; avoid --value on the command line (shell history).
$secure = Read-Host -AsSecureString "YouTube Data API key"
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try {
  $ytKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
  $tmp = [System.IO.Path]::GetTempFileName()
  try {
    [System.IO.File]::WriteAllText($tmp, $ytKey)
    az keyvault secret set `
      --vault-name $KV `
      --name "RecipeImport-YouTube-ApiKey" `
      --file $tmp `
      --encoding utf-8 | Out-Null
  }
  finally {
    Remove-Item -Force $tmp -ErrorAction SilentlyContinue
  }
}
finally {
  [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
  $ytKey = $null
  $secure = $null
}

$REV = (az containerapp revision list -g $RG -n $CA --query "[0].name" -o tsv)
az containerapp revision restart -g $RG -n $CA --revision $REV
```

---

## C. Lokaal

Use `.env` (gitignored) or user secrets — see `.env.example`. Do not put the key
in `appsettings*.json`.

```
RecipeImport__YouTube__ApiKey=AIza...
```

---

## D. Verificatie

```bash
curl -X POST "https://<container-app-fqdn>/recipes/import-url" \
  -H "Content-Type: application/json" \
  -d '{"url":"https://www.youtube.com/shorts/DSGRNoSTvLg","parseOptions":{"useAiFallback":false}}'
```

Verwacht: titel met Panang Curry, meerdere ingredients/steps — géén
cookie-consenttekst (“Innan du fortsätter till YouTube” / “Bevor Sie zu YouTube
weitergehen”).
