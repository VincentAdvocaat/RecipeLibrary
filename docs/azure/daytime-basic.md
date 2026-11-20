# SEC: daytime warm app and SQL Basic

Approved on 20 November 2026: keep the existing app warm daily 08:00–20:00 local
time and move its existing SQL database to Basic. The SQL free-offer constraint
from earlier work is superseded by this explicit paid-plan decision for SEC.

## Schedule and initialization

SEC passes `enableDaytimeWarmup=true` and `useBasicSql=true` to `main.bicep`.
One built-in KEDA cron scaler uses `Europe/Paris`, start `59 7 * * *`, end
`0 20 * * *`, desiredReplicas `1`. The one-minute prewarm margin allows the
measured approximately 42-second container startup before 08:00. Initial schema
updates or infrastructure failures can take longer; this isn't an availability
guarantee. TZ database rules retain the local hours across daylight-saving changes.
Weekends use the same schedule. No extra scheduler resource is deployed.

`minReplicas=0`, `maxReplicas=1` and the HTTP scaler remain. After 20:00 the cron
floor disappears; the container can scale to zero after normal idle/cooldown time.
An actual nighttime visitor can still wake it. This is not a hard nighttime stop.

Basic enables `PersistenceWarmup:AutoStart=true`. Existing bounded, asynchronous,
single-flight initialization runs as the container starts, rather than waiting for
the first visitor. Its diagnostics use reason `startup`, correlation label
`startup`, a distinct attempt ID and SQL path `/background`; they must not be
reported as visitor clicks. Passive readiness handlers still do no SQL I/O.
The option defaults false in .NET and Bicep; optional NEU/free-serverless retains
explicit-start behavior and no cron rule.

## Database conversion

Basic provides 5 DTU and a 2 GiB maximum. It stays online and is billed at its
fixed rate overnight too; the app schedule does not pause the database. The same
server, database, managed identity, users, grants and connection string are kept.
No recipe/account data is dropped or exported to another provider. Existing
seven-day short backup retention and blob storage remain.

Preflight metrics on 20 November: approximately 27.7 MB used, 33.6 MB allocated
(26.4 / 32 MiB), comfortably below the limit. The checked-in script rechecks recent
allocation metrics and requires substantial headroom before applying anything:

```powershell
./scripts/switch-sql-to-basic.ps1          # management-only dry-run
./scripts/switch-sql-to-basic.ps1 -Apply   # approved one-time conversion
```

The script uses a fixed target/subscription, verifies the expected source SKU,
and permanently removes the free offer before switching the same database to
Basic. It waits for each update and verifies the final plan. If conversion fails
after free-offer removal, paid General Purpose might remain: inspect and repair
the existing database promptly; do not blindly retry or create/delete databases.
The free offer cannot be restored to this database. This is the approved move
to Basic, not the previously declined temporary paid-overage experiment.

Review and run conversion before merging the app rollout: SEC's deployment
validation requires Basic 5 DTU / <=2 GiB and rejects an unexpected plan instead
of upgrading SQL automatically. Ordinary releases still skip the SQL module.
Intentional provisioning retains `provisionSqlInfrastructure=true`; for manual
SEC Bicep calls also pass `useBasicSql=true`. Other environments default to the
free-serverless configuration. Never use Complete deployment mode.

## Cost guard and verification

Basic costs approximately EUR 4.50/month excluding VAT at the quoted regional
rate. With twelve daytime warm hours and little traffic, estimate approximately
EUR 6–8/month for SQL plus this small container, depending on remaining shared
free grants. Other services, active compute and requests can add charges.

Update the existing resource-group budget from EUR 5 to EUR 12 without changing
its receivers or thresholds: warning at 50%, existing app-stop action at 80%,
and actual/forecast notices at 100%. Budgets are delayed alerts/actions, not a
hard spending cap; stopping the app does not remove Basic's fixed SQL bill.
No paid scheduler or new analytics service is required.

Before release: compiled ARM tests for both SQL plans, default-off SQL deployment,
cron/IANA/DST config and unchanged connection names; integration tests for
automatic single-flight initialization, passive-free defaults and correct startup
analytics. After release: verify Basic/Online, cron metadata and ready revision,
readiness Ready without a start POST, demo recipes in a clean browser, private
analytics access and `startup` diagnostic attribution. Check the existing
budget amount/notifications. Verify actual night scale-to-zero in a later
observation; a daytime check alone cannot establish that it happened at night.

Sources: [Microsoft time-based scaling](https://techcommunity.microsoft.com/blog/appsonazureblog/configure-time-based-scaling-in-azure-container-apps/4398327),
[DTU limits](https://learn.microsoft.com/en-us/azure/azure-sql/database/resource-limits-dtu-single-databases?view=azuresql),
[permanent free-offer removal](https://learn.microsoft.com/en-us/azure/azure-sql/database/free-tier-upgrade-to-hyperscale?view=azuresql),
[Container Apps billing](https://learn.microsoft.com/en-us/azure/container-apps/billing).
