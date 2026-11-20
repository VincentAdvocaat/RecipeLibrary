# Startup measurement

Current SEC uses the [daytime Basic plan](daytime-basic.md). Its automatic
initialization means waking a container also initializes SQL. The container-only
probe now refuses that mode before sending HTTP; do not disable the live plan
merely to reproduce the historical SQL-free baseline below. The daytime cron
also intentionally prevents scale-to-zero during its active window.

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

## Live baseline: 20 November 2026

Measured from the operator's Windows machine, 10:38–10:57 CET (09:38–09:57 UTC).
All samples used revision `ca-rl-test-sec-gdqfvo--0000036`, 0.25 vCPU / 0.5 GiB,
minimum zero / maximum one replica. No concurrent deployment occurred. Each cold
sample had a fresh management observation of zero replicas. All six health
requests returned HTTP 200 without retries. SQL was Paused, useFreeLimit=true,
AutoPause before the run and after every sample. The SQL Activity Log contained
zero resume events during the measurement window.

| Sample | Cold health response | Immediate warm response |
|---|---:|---:|
| 1 | 41.937 s | 0.385 s |
| 2 | 41.532 s | 0.399 s |
| 3 | 41.804 s | 0.354 s |
| Median | **41.804 s** | **0.385 s** |
| Slowest | 41.937 s | 0.399 s |

Cold DNS/TCP/TLS completion took 0.286–0.557 s in total; almost all remaining
time was spent awaiting the first response. These are measurements of the whole
cold service path, not evidence that the .NET process itself takes 42 seconds.
Natural return to zero replicas took roughly 7–8 minutes between these samples;
that idle waiting time is excluded from the HTTP startup duration.

### Browser pages, backend already warm

Three fresh Chromium contexts visited the static welcome and then the backend
welcome/start page. No start button was clicked and zero POST requests to
`/api/system/start` occurred. Browser load timings include assets and differ from
the tiny health response. The first browser run also includes initial client
connection/cache effects; no claim is made that it represents an isolated server
rendering duration.

| Page | Median first byte | Median load complete | Slowest load complete |
|---|---:|---:|---:|
| Static welcome | 0.485 s | 0.664 s | 4.016 s |
| Backend welcome/start page | 0.327 s | 1.309 s | 2.032 s |

Median first contentful paint: static 0.672 s, backend 1.132 s. Load completion
and first paint are different milestones; paint can occur after the load event.
The backend's visible startup control was verified, not a usable recipe screen.

### Supporting platform timestamps for sample 3

The zero-replica observation was at 09:55:29.228 UTC. Platform events show the
application image pull at 09:55:45–46 UTC (reported pull duration 588 ms, image
size 161,090,770 bytes), then application container start at 09:55:46 UTC. The
console logged HTTP listening / application started at approximately 09:55:54.864
UTC. The client finished the health response at 09:56:11.078 UTC.

This single sample suggests roughly 16 seconds before image pull, 9 seconds from
container start to listening, and another 16 seconds from listening to the client
response. These are approximate intervals across clocks and second-resolution
platform events. They do not prove which scheduler, secret resolution, readiness
or ingress step accounts for every interval. Startup/readiness probes currently
run at 10-second periods; their impact is a candidate for a separate controlled
experiment. Image download was under one second in this sample, so shrinking the
image alone is not supported as the main fix by this observation.

### Limits and next decision

The entire live run used the original probe implementation at `17da428`; no
personal curl configuration was found in the local default search locations and
all SQL metadata reads succeeded. Follow-up fixes in the same PR explicitly
disable curl's personal configuration and save completed HTTP timings before the
additional metadata read; offline checks cover both fixes. They did not change
the running process or target environment.

Raw selected timing JSON, clean-browser results, and filtered platform evidence
are retained in the operator's local workspace under
`.codex/reviews/RecipeLibrary/startup*-2026-11-20.json`; no credential or cookie
values were included in the evidence.

The free SQL allowance was exhausted and the user explicitly declined paid
continuation. Therefore cold-SQL, fully warm recipe access, migrations/seed time,
and click-to-usable-recipe duration were **not measured**. Run those stages after
the free allowance renews; do not substitute the current free-limit failure time
for successful SQL startup. No paid SQL use, runtime code, hosting configuration
or database settings were enabled/changed for this benchmark.

Conclusion: a repeatable approximately 42-second cold container/service delay
exists independently of SQL. Replacing SQL alone leaves that measured delay.
First investigate the platform/HTTP-ready gate and process startup milestones;
compare any proposed improvement against this baseline before buying permanent
warm capacity or changing database technology.
