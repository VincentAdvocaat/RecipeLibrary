# Azure subscription bootstrap for Azure DevOps

Use this checklist to prepare a new Azure subscription for deployment from an
Azure DevOps pipeline. It intentionally contains placeholders instead of
environment-specific names, IDs, or credentials.

> Replace every `<placeholder>` with the actual value, without the `<` and `>`
> characters. Run privileged commands with a personal account that is Owner (or
> has equivalent permissions) on the target scope, not as the pipeline service
> principal.

## Prerequisites

- An Azure tenant and billing account
- Permission to create or activate a subscription
- Owner permissions on the subscription or target resource group
- An Azure DevOps project and pipeline
- Azure CLI installed, or access to Azure Cloud Shell

## 1. Create and select the subscription

Create the subscription in the Azure portal:

1. Open **Subscriptions**.
2. Select **Add / Create a subscription**.
3. Choose the billing scope and offer.
4. Enter a subscription name and complete the creation.
5. Wait until its state is **Enabled**.

Sign in and select the subscription:

```bash
az login
az account set --subscription "<subscription-id>"
az account show --query "{name:name, id:id, tenantId:tenantId, state:state}" -o table
```

Record these non-secret identifiers for the Azure DevOps service connection:

- Subscription ID
- Subscription name
- Tenant ID

## 2. Register the required resource providers

Resource providers are registered once per subscription. Register all namespaces
used by the Bicep template before the first deployment:

```bash
az provider register --namespace Microsoft.Sql --subscription "<subscription-id>"
az provider register --namespace Microsoft.App --subscription "<subscription-id>"
az provider register --namespace Microsoft.Storage --subscription "<subscription-id>"
az provider register --namespace Microsoft.KeyVault --subscription "<subscription-id>"
az provider register --namespace Microsoft.OperationalInsights --subscription "<subscription-id>"
```

Registration is asynchronous and can take several minutes. Wait until every
provider reports `Registered`:

```bash
az provider show --namespace Microsoft.Sql --subscription "<subscription-id>" --query registrationState -o tsv
az provider show --namespace Microsoft.App --subscription "<subscription-id>" --query registrationState -o tsv
az provider show --namespace Microsoft.Storage --subscription "<subscription-id>" --query registrationState -o tsv
az provider show --namespace Microsoft.OperationalInsights --subscription "<subscription-id>" --query registrationState -o tsv
```

The deployment may also use the already registered platform namespaces
`Microsoft.Resources` and `Microsoft.Authorization`.

## 3. Create the target resource group

Choose an eligible Azure region before creating the resource group. Resource
groups cannot be moved to another region after creation.

```bash
az group create \
  --name "<resource-group-name>" \
  --location "<azure-region>" \
  --subscription "<subscription-id>"
```

Verify it:

```bash
az group show \
  --name "<resource-group-name>" \
  --subscription "<subscription-id>" \
  --query "{name:name, location:location, state:properties.provisioningState}" \
  -o table
```

## 4. Create the Azure DevOps service connection

Workload identity federation is preferred because it does not require a client
secret.

1. In Azure DevOps, open **Project settings → Service connections**.
2. Select **New service connection → Azure Resource Manager**.
3. Select **Workload identity federation (automatic)**.
4. Scope the connection to:
   - the target subscription; and
   - the target resource group.
5. Enter a clear service connection name.
6. Save and authorize the connection for the required pipeline only. Avoid
   granting access to all pipelines unless that is intentional.

Azure DevOps creates or links an Entra service principal. Record its **object
ID**, not only its application/client ID.

If only the application/client ID is visible, resolve the service principal
object ID:

```bash
az ad sp show \
  --id "<application-client-id>" \
  --query "{displayName:displayName, objectId:id, applicationId:appId}" \
  -o table
```

## 5. Grant Contributor on the resource group

The pipeline service principal needs Contributor to create and update the
application infrastructure. Keep this assignment at resource-group scope.

```bash
az role assignment create \
  --assignee-object-id "<service-principal-object-id>" \
  --assignee-principal-type ServicePrincipal \
  --role "Contributor" \
  --scope "/subscriptions/<subscription-id>/resourceGroups/<resource-group-name>"
```

Contributor cannot create Azure RBAC role assignments. If Bicep assigns a
data-plane role to a managed identity, add the restricted role from the next
step as well.

## 6. Grant restricted RBAC administration

The following assignment lets the pipeline service principal assign or remove
only these data-plane roles inside the resource group:

- Storage Blob Data Contributor (`ba92f5b4-2d11-453d-a403-e96b0029c9fe`)
- Key Vault Secrets User (`4633458b-17de-408a-b874-0445c86b69e6`)
- Key Vault Secrets Officer (`b86a8fe4-44ce-4948-aee5-eccb2c155cd7`)

It cannot use this assignment to grant Owner or Contributor.

### Bash / Azure Cloud Shell

```bash
az role assignment create \
  --assignee-object-id "<service-principal-object-id>" \
  --assignee-principal-type ServicePrincipal \
  --role "Role Based Access Control Administrator" \
  --scope "/subscriptions/<subscription-id>/resourceGroups/<resource-group-name>" \
  --condition "((!(ActionMatches{'Microsoft.Authorization/roleAssignments/write'})) OR (@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {ba92f5b4-2d11-453d-a403-e96b0029c9fe, 4633458b-17de-408a-b874-0445c86b69e6, b86a8fe4-44ce-4948-aee5-eccb2c155cd7})) AND ((!(ActionMatches{'Microsoft.Authorization/roleAssignments/delete'})) OR (@Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {ba92f5b4-2d11-453d-a403-e96b0029c9fe, 4633458b-17de-408a-b874-0445c86b69e6, b86a8fe4-44ce-4948-aee5-eccb2c155cd7}))" \
  --condition-version "2.0"
```

### PowerShell

```powershell
az role assignment create `
  --assignee-object-id "<service-principal-object-id>" `
  --assignee-principal-type ServicePrincipal `
  --role "Role Based Access Control Administrator" `
  --scope "/subscriptions/<subscription-id>/resourceGroups/<resource-group-name>" `
  --condition '((!(ActionMatches{''Microsoft.Authorization/roleAssignments/write''})) OR (@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {ba92f5b4-2d11-453d-a403-e96b0029c9fe, 4633458b-17de-408a-b874-0445c86b69e6, b86a8fe4-44ce-4948-aee5-eccb2c155cd7})) AND ((!(ActionMatches{''Microsoft.Authorization/roleAssignments/delete''})) OR (@Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {ba92f5b4-2d11-453d-a403-e96b0029c9fe, 4633458b-17de-408a-b874-0445c86b69e6, b86a8fe4-44ce-4948-aee5-eccb2c155cd7}))' `
  --condition-version "2.0"
```

Do not add a backtick after the final PowerShell line.

## 7. Verify the role assignments

```bash
az role assignment list \
  --assignee "<service-principal-object-id>" \
  --scope "/subscriptions/<subscription-id>/resourceGroups/<resource-group-name>" \
  --query "[].{role:roleDefinitionName, scope:scope, condition:condition}" \
  -o table
```

Expected assignments:

- `Contributor`
- `Role Based Access Control Administrator`, with the Storage Blob Data
  Contributor condition

## 8. Configure Azure DevOps pipeline values

Configure identifiers and sensitive values in Azure DevOps pipeline variables or
an authorized variable group. Do not commit credentials, access tokens, client
secrets, connection strings, personal UPNs, or environment IDs to the repository.

Typical values include:

- Tenant ID
- Entra SQL administrator login
- Entra SQL administrator object ID
- Azure service connection name
- Resource group name
- Azure region
- GHCR username and token (for pipeline image publish)

Mark sensitive values as secret. Prefer workload identity federation over a
service principal client secret.

## 9. Run and verify the first deployment

1. Run the pipeline on `main`.
2. Confirm the Azure service connection logs into the expected subscription.
3. Confirm the Bicep deployment succeeds (Container App, SQL, storage).
4. Apply database-internal grants for the user-assigned managed identity (`docs/azure/sql-grants.sql`).
5. Re-run the pipeline and confirm the smoke check returns HTTP `200` for `/health` and `/`.
6. Verify the managed identity received Storage Blob Data Contributor on the storage account.
7. Deploy `infra/cost-guard.bicep` separately with an Owner account (optional but recommended).

## Troubleshooting

- `MissingSubscriptionRegistration`: register the resource provider from step 2
  and wait for `Registered`.
- `RequestDisallowedByAzure` / `locationineligible`: choose a region available to
  the subscription and create a new resource group there.
- `Microsoft.Authorization/roleAssignments/write`: verify the restricted Role
  Based Access Control Administrator assignment from step 6.
- `AuthorizationFailed`: verify the service principal object ID, assignment
  scope, and service connection subscription.
- `RoleAssignmentExists`: the assignment is already present; verify it in step 7
  instead of creating another one.

## Security checklist

- Use one dedicated service principal per environment or trust boundary.
- Use workload identity federation; do not store a client secret in the repo.
- Scope Contributor and RBAC administration to the resource group.
- Restrict RBAC administration with a role condition.
- Keep subscription Owner access out of the pipeline.
- Review service principal access regularly and remove unused assignments.
