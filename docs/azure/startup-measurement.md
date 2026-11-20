# Startup measurement

## Repeat the container-only probe

Run outside deployment windows, while signed into Azure CLI with management read
permissions. Requires Python 3 and curl; there are no additional packages or paid
monitoring services.

```powershell
python scripts/measure-startup.py --output "$env:TEMP/recipelibrary-startup.json" --samples 3
```

The script targets the existing SEC app's custom hostname and GET `/health/live`
only. Before each cold request it verifies through Azure management metadata that
exactly one active revision exists with zero replicas. It then makes one cold and
one immediate warm request using separate curl processes. There are no HTTP
retries, redirects, database connections, POST start actions, configuration
changes or forced scale operations. It waits for natural scale-to-zero, querying
management metadata every 30 seconds, for at most 20 minutes per sample.

Results are saved after each sample. Timings include DNS, TCP, TLS, first byte
and complete response time, with HTTP status, revision, zero-replica observation
time, and SQL metadata state before/after. Only these selected fields are saved;
no keys, cookies, remote addresses or user identifiers are retained. A failed
request remains a failure rather than being replaced by a later successful retry.

Do not visit the app or run HTTP monitors while waiting: browser analytics beacons
can wake the backend even when only opening the static welcome site. Other public
visitors can invalidate a cold sample, so the zero-replica observation is evidence
immediately before the request, not exclusive control of the public environment.
Samples from different revisions must not be combined as one benchmark.

This measures the cold HTTP service path, including platform scheduling/image
startup and application initialization up to the health response. It is not an
isolated .NET process boot duration and does not establish database readiness.
Compare median and slowest response across three samples, not just the fastest.

## Browser and full application stages

After the container samples, measure a clean Chromium browser context for each
visit to the static welcome and backend welcome pages. Record navigation first
byte, DOMContentLoaded, load and first contentful paint, and assert that no POST
`/api/system/start` occurs. This validates page availability with the server warm;
it does not establish that recipes or authentication are ready while SQL sleeps.

The full application test remains separate: user click, accepted start request,
SQL connection, migration/seed completion, then a usable recipe screen. Existing
analytics RequestId/AttemptId and warmup duration logs can correlate these phases;
do not collect account data, raw SQL or connection strings. Run warm-container /
cold-SQL, cold-container / cold-SQL and fully warm scenarios three times only when
the monthly free capacity permits. Do not switch to paid overage to finish this
test: Microsoft documents that the monthly-limit auto-pause option cannot be
restored after choosing continued paid use.

Sources: [SQL free tier limitations](https://learn.microsoft.com/en-us/azure/azure-sql/database/free-tier?view=azuresql),
[Container Apps scaling](https://learn.microsoft.com/en-us/azure/container-apps/scale-app).
