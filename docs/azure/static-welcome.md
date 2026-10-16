# Static welcome page and cold starts

## Behavior

The public entrance is `https://recipelibrary.vasoftware.nl`. It serves plain HTML,
CSS and JavaScript from Azure Static Web Apps Free, independently of Container Apps.
The visitor immediately sees Dutch welcome text, with an English language button.
The browser polls the backend and replaces the page with
`https://app-recipelibrary.vasoftware.nl` when SQL is reachable. Until public domain
binding is complete, deployments use the generated Container Apps hostname instead.

Only visitor requests wake the backend. No timer, Azure health probe or background
availability check keeps SQL alive. Container Apps still allows zero replicas; SQL
still auto-pauses after 60 minutes. Direct app links still incur container startup.
To share a recipe through the instant entrance, use
`https://recipelibrary.vasoftware.nl/?returnUrl=%2Frecipes%2FRECIPE_ID`.

The frontend has no access to account data. Authentication stays on the app host;
existing Azure-host sessions do not transfer to the new hostname. Users sign in once
on the new hostname. Cookie Domain must remain unset, never `.vasoftware.nl`.

## Application contract

`GET /api/system/readiness` is anonymous and returns HTTP 200 with
`{ "state": "Starting|Ready|Failed", "retryAfterSeconds": 5|null, "traceId": "..." }`.
Responses are `Cache-Control: no-store`. CORS permits GET only from the exact welcome
custom domain and the generated static site origin, without credentials.

The one-time migration/seeding state is independent of current SQL availability.
Initial migration and seeding run in a BackgroundService, with a five-minute
cancellation deadline. `Testing` retains synchronous setup. Once initialization
succeeds, visitor readiness requests run one shared `SELECT 1` probe, with a
seven-second cancellation deadline and five-second result cache. EF retries are not
used for these probes. Temporary SQL failures return `Starting` and can recover
without restarting the process. There is no recurring SQL probe.

`/health/live`, `/health/ready` and the `/health` alias check the HTTP process only.
`/health/database` continues to expose the initial migration state for operators;
use `/api/system/readiness` for current SQL reachability.

The browser sends one request at a time, with a ten-second timeout and five seconds
between completed attempts. It stops after five minutes or `Failed`; Retry starts
a new round. Retry does not rerun permanently failed migrations. Operators must
correct configuration or redeploy for those failures. Exhausted SQL free allowance
and intentional hibernation require operator action. Internal exception details
remain in server logs.

`/starting` and `/failed` use static SSR with the same browser module and no Blazor
circuit. Startup redirects preserve a validated local `returnUrl`. Non-GET/HEAD
requests blocked during startup return 503, never redirect or replay submitted data.

## First deployment and DNS

1. Merge the reviewed PR. The main Azure DevOps pipeline deploys SEC infrastructure,
   the backend and the welcome site. Leave `configurePublicDomains` false initially.
   The existing `RecipeLibrary-Azure-SEC` service connection needs permission to create
   `Microsoft.Web/staticSites` and read its deployment token. Register the
   `Microsoft.Web` provider if not already registered. No DNS-zone migration is needed.
2. The pipeline prints all three required DNS records. The Bicep outputs include
   `welcomeSiteHostname`, `containerAppFqdn` and `appDomainVerificationId`. Test the
   generated static site URL before binding public domains. CORS already includes it.
3. In the existing DNS account serving `ns.zxcs.nl`, `ns.zxcs.be`, `ns.zxcs.eu`, add
   the records below. Use TTL 300 during rollout. Do not change apex, `www`, mail or
   other existing website records. Resolve any conflicting record only for the two
   new labels. Keep the records in place for certificate renewal.

| Type | Name relative to vasoftware.nl | Value |
| --- | --- | --- |
| CNAME | `recipelibrary` | `welcomeSiteHostname` output, without protocol |
| CNAME | `app-recipelibrary` | `containerAppFqdn` output, without protocol |
| TXT | `asuid.app-recipelibrary` | `appDomainVerificationId` output |

Observed backend values on 2026-10-16, **recheck deployment outputs before use**:

```text
app-recipelibrary CNAME ca-rl-test-sec-gdqfvo.happycliff-3e96af65.swedencentral.azurecontainerapps.io
asuid.app-recipelibrary TXT 2ADD8099F45630FB03C67418543A992126041BC1624C3B9E54CE7CFF1FE01271
```

The static site hostname is allocated on the first deployment and cannot be supplied
in advance. If DNS account access is unavailable, hand these exact deployment-output
records to its owner; no other access or secrets are needed.

4. Wait for public DNS to resolve correctly. Run the main pipeline manually with
   `configurePublicDomains: true`. The domain script refuses to run outside SEC,
   verifies both CNAMEs and the TXT value, then adds managed TLS bindings. Azure
   certificate issuance can take time. A failed validation leaves the previous
   deployed frontend available; fix DNS and rerun rather than bypassing HTTPS checks.
5. The pipeline selects the custom backend URL only after an SNI binding exists. It
   validates HTTPS on that host before publishing the frontend. Check both custom
   addresses in a browser, sign in, open a recipe and exercise a Blazor interaction.
   Confirm logout works and authentication cookies are host-only.

The deployment token is read through Azure CLI and passed as a masked pipeline
secret to `AzureStaticWebApp@0`. It is never emitted in Bicep outputs or included in
frontend files. Build configuration contains only a public backend origin.

Existing app hostname/certificate bindings are captured before each regional Bicep
deployment and passed back as `appCustomDomains`. **Manual Bicep deployments must
also supply the existing bindings** or ARM will clear them. NEU does not provision
the welcome site or claim the public domains. The static site remains deployed when
the existing hibernation pipeline removes Container Apps compute.

## Validation and cold-start acceptance

Local checks from the worktree:

```powershell
node --test tests/welcome/startup.test.mjs
node scripts/build-welcome.mjs
dotnet build RecipeLibrary.slnx -c Release
dotnet test tests/RecipeLibrary.Application.Tests -c Release --no-build
dotnet test tests/RecipeLibrary.Web.ComponentTests -c Release --no-build
dotnet test tests/RecipeLibrary.Web.IntegrationTests -c Release --no-build
./tests/RecipeLibrary.Web.E2E.Tests/bin/Release/net10.0/playwright.ps1 install chromium
dotnet test tests/RecipeLibrary.Web.E2E.Tests -c Release --no-build --filter FullyQualifiedName~WelcomePageTests
az bicep build --file infra/main.bicep --stdout
```

The integration suite needs Docker for SQL Testcontainers. Welcome browser tests
and startup HTTP integration tests use local fakes and do not require Azure or SQL.
CI runs unit, component, integration, JavaScript and welcome browser tests.

After rollout, close app tabs and allow natural inactivity. Do not poll HTTP
health/readiness while waiting, since requests can wake the container. Check status
through the Azure management API: `az containerapp replica list` must show no replicas
and `az sql db show ... --query status` must report `Paused`. Do not use hibernation
as a substitute: it deletes compute and will not wake on a visitor request.

Open the welcome URL in a browser with Network recording and Preserve log enabled.
Record timestamps for visible static content, the first successful HTTP response
from the backend and the first readiness response with `Ready`. Keep the window open
and verify automatic navigation to the requested recipe or login with ReturnUrl.
Record browser, connection, resource states and elapsed times. Do not add overlapping
measurements together. Confirm visible content within two seconds on three ordinary
page visits; only the first needs to trigger a cold start. A second complete cold
test requires natural inactivity again.

Also verify offline backend, timeout/Retry, permanent initialization failure, invalid
JSON/temporary ingress error, mobile layout, keyboard navigation, language switching
and a no-JavaScript direct link. No artificial progress percentages are displayed.

## Recovery and rollback

- If the frontend fails, rerun the pipeline at the previous reviewed commit to
  republish its static files. The generated backend URL remains a direct fallback.
- If the backend fails, activate the previous Container Apps revision and route
  traffic to it. Keep the original Data Protection application name/key store.
- No database schema migration is added by this change. Never reverse application
  migrations or remove recipe data to repair the welcome page.
- After hibernation and redeployment, recheck the app FQDN and verification ID. A
  recreated environment can change DNS targets. Update only the listed app CNAME/TXT
  records and rerun with `configurePublicDomains: true` to restore managed TLS.
- After renewal or redeployment, verify HTTPS and the readiness CORS response again.
  Routine Azure process probes must continue to avoid SQL entirely.
