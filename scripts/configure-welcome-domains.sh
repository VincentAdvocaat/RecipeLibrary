#!/usr/bin/env bash
# Run only after the two CNAME records and the ACA verification TXT record resolve publicly.
set -euo pipefail
rg="${1:?resource group required}"
app="${2:?container app required}"
site="${3:?static site required}"
if [[ "$rg" != "rg-recipelibrary-test-sec" ]]; then
  echo 'Public RecipeLibrary domains are reserved for SEC.' >&2
  exit 1
fi
app_domain=app-recipelibrary.vasoftware.nl
welcome_domain=recipelibrary.vasoftware.nl
app_fqdn="$(az containerapp show -g "$rg" -n "$app" --query properties.configuration.ingress.fqdn -o tsv)"
verification="$(az containerapp show -g "$rg" -n "$app" --query properties.customDomainVerificationId -o tsv)"
site_fqdn="$(az staticwebapp show -g "$rg" -n "$site" --query defaultHostname -o tsv)"
for record in "$app_domain:$app_fqdn" "$welcome_domain:$site_fqdn"; do
  domain="${record%%:*}"
  expected="${record#*:}"
  actual="$(dig +short CNAME "$domain" | sed 's/\.$//')"
  if [[ "$actual" != "$expected" ]]; then
    echo "DNS pending: CNAME $domain -> $expected (found: $actual)" >&2
    exit 1
  fi
done
if ! dig +short TXT "asuid.$app_domain" | tr -d '"' | grep -Fxq "$verification"; then
  echo "DNS pending: TXT asuid.$app_domain -> $verification" >&2
  exit 1
fi
binding="$(az containerapp hostname list -g "$rg" -n "$app" --query "[?name=='$app_domain'].bindingType | [0]" -o tsv)"
if [[ "$binding" != 'SniEnabled' ]]; then
  if [[ -z "$binding" ]]; then
    az containerapp hostname add -g "$rg" -n "$app" --hostname "$app_domain" -o none
  fi
  environment_id="$(az containerapp show -g "$rg" -n "$app" --query properties.managedEnvironmentId -o tsv)"
  az containerapp hostname bind -g "$rg" -n "$app" --hostname "$app_domain" \
    --environment "$environment_id" --validation-method CNAME -o none
fi
status="$(az staticwebapp hostname list -g "$rg" -n "$site" --query "[?domainName=='$welcome_domain'].status | [0]" -o tsv)"
if [[ "$status" != 'Ready' ]]; then
  az staticwebapp hostname set -g "$rg" -n "$site" --hostname "$welcome_domain" \
    --validation-method cname-delegation -o none
fi
curl --fail --silent --show-error --max-time 60 "https://$app_domain/health/live"
curl --fail --silent --show-error --max-time 60 "https://$welcome_domain/" -o /dev/null
