# Content moderation (E8)

Automated checks for user-generated recipe text and images, prepared for a future public release. Libraries remain private; moderation status and admin tooling are ready when you turn the feature on.

## Feature flag (required)

| Setting | Purpose |
|---------|---------|
| `ContentModeration:Enabled` | Master switch. **Default `false`** — no Azure calls, create/update/upload behave as before. |
| `ContentModeration:Endpoint` | Azure AI Content Safety endpoint |
| `ContentModeration:ApiKey` | API key (prefer Key Vault / env in deployed environments) |
| `ContentModeration:BlockSeverityThreshold` | Severity ≥ this value **blocks** persist (default `4`) |
| `ContentModeration:ReviewSeverityThreshold` | Severity ≥ this (and below block) → `NeedsReview` (default `2`) |
| `ContentModeration:AdminEmails` | Emails granted the `Admin` Identity role at startup |

Environment variable form:

```text
ContentModeration__Enabled=false
ContentModeration__Endpoint=https://{resource}.cognitiveservices.azure.com/
ContentModeration__ApiKey=...
ContentModeration__AdminEmails__0=you@example.com
```

When `Enabled` is `true` but endpoint/key are missing, the app registers a **null moderator** (no provider calls). Treat that as misconfiguration for production.

## Policy (spike F9/F10)

| Decision | Choice |
|----------|--------|
| Provider | Azure AI Content Safety (text + image) |
| Text scanned | Title, description, ingredient name/preparation, instruction steps |
| Images | Moderated on upload **before** storage (no orphan blobs on block) |
| Severity ≥ 4 | Block — content is not saved; user sees a localized rejection message |
| Severity 2–3 | Save with `NeedsReview` |
| Severity 0–1 | Save with `Approved` |
| Flag off | Status `NotModerated`; no provider calls |

## Admin review

1. Set `ContentModeration:AdminEmails` to existing user emails (Development seed user is fine locally).
2. Restart the app so the hosted service creates the `Admin` role and assigns it.
3. Open `/admin/moderation` (nav item “Moderation” appears for Admins only).
4. Approve or reject recipes in `NeedsReview`, and handle open user reports.

## User reports

On a recipe detail page, **Report** creates a `ContentReport` and flags the recipe for review when it was not already rejected / in review.

## Enabling later (ops)

1. Provision an Azure AI Content Safety resource.
2. Store the key in Key Vault (same pattern as OpenAI — see `docs/azure/openai-keyvault.md`).
3. Set Container App / App Service env: `ContentModeration__Enabled=true`, `ContentModeration__Endpoint`, `ContentModeration__ApiKey` (or Key Vault reference).
4. Restart — no code redeploy of the feature itself is required.

Keep `Enabled=false` in deployed defaults until you intentionally go live.
