#!/usr/bin/env bash
# One-time: grant the Web App managed identity access to recipe image blobs.
# Requires Azure CLI login with permission to create role assignments
# (User Access Administrator or Owner on the storage account / resource group).
set -euo pipefail

resource_group="${1:-rg-recipelibrary-test-weu}"
web_app_name="${2:?Usage: $0 [resource-group] <web-app-name>}"

principal_id="$(az webapp identity show \
  --resource-group "$resource_group" \
  --name "$web_app_name" \
  --query principalId -o tsv)"

storage_id="$(az storage account list \
  --resource-group "$resource_group" \
  --query "[?starts_with(name, 'st')].id | [0]" -o tsv)"

if [[ -z "$storage_id" || "$storage_id" == "null" ]]; then
  echo "No storage account found in $resource_group" >&2
  exit 1
fi

az role assignment create \
  --assignee-object-id "$principal_id" \
  --assignee-principal-type ServicePrincipal \
  --role "Storage Blob Data Contributor" \
  --scope "$storage_id"

echo "Granted Storage Blob Data Contributor to $web_app_name on $storage_id"
