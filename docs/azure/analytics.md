# Analytics deployment

`infra/analytics.bicep` creates one dedicated Cosmos DB for NoSQL account in Sweden Central, one `analytics` database and one `traffic` container partitioned on `/day`. It can be validated or deployed independently with `accountName` and the existing application's managed identity `principalId` parameters. Deploy only once per subscription, in SEC. The optional NEU deployment keeps analytics disabled.

`main.bicep` defaults `enableAnalytics` to false. The SEC pipeline defaults its `enableAnalytics` parameter to true. Microsoft.DocumentDB registration is performed once by a subscription administrator; the resource-group deployment identity needs no subscription provider permissions. ARM deployment fails if registration is missing. The subscription's unused Free Tier slot is reserved for this account. Deployment must fail if Free Tier cannot be enabled.

## Cost boundaries

The account requires the subscription's unused Free Tier slot and explicitly enables Free Tier. There is no fallback to a paid account if the slot is occupied. The account has one region, no zone redundancy, manual container throughput of 400 RU/s and an account throughput limit of 1,000 RU/s. There is no serverless or autoscale configuration. Periodic backups run every four hours with eight-hour retention, retaining the two free backup copies. Analytics adds no Application Insights, Log Analytics, blob storage, Functions or deployment scripts.

The application uses at most one item per UTC hour and keeps it for 31 days from that hour, including when it is updated. Each item has at most 200 detailed events, 100 entries per aggregate dimension, and 100 writer sequence markers. Writes exceeding one million serialized bytes per hourly item are rejected. This limits the raw item payload to less than 1 GB, including a possible partial boundary hour. Requests, bodies and dimension cardinality are bounded before recording. Storage failures and rate limits drop telemetry instead of blocking the application. The dashboard exposes dropped and sampled counts. No request-controlled partition keys or timestamps are used.

The dashboard returns at most 200 event details and the 12 largest dimension values per hour. Historical aggregate totals include events outside the detail sample. Referrer data describes the referring website where provided; user agent names and daily IP pseudonyms are indications, not verified identities or a geographic lookup.

SQL connection events cover Entity Framework connection boundaries. Explicit start and availability probes have separate lifecycle events. The demo's independently owned SQL lock session is not intercepted, so these events are not a complete count of physical SQL connections. Use Azure SQL metrics for the service-wide connection total.

Free Tier includes 1,000 RU/s and 25 GB storage. Cosmos does not provide a hard storage spending cap. The throughput limit does not cap storage charges, and Azure can raise throughput above the limit if stored data requires a higher minimum. The application's bounded storage is therefore essential. Keep the existing resource-group budget and cost guard in place. That guard stops the Container App, not Cosmos DB, and is not a substitute for these limits. Other existing infrastructure charges remain subject to their existing budgets.

## Identity and dashboard key

Local Cosmos authentication is disabled. The existing user-assigned application identity receives Cosmos DB Built-in Data Contributor only at `/dbs/analytics`, with no account-wide data grant. Cosmos uses its public TLS endpoint with Entra authentication. No private endpoint or additional paid networking is created.

The app receives `Analytics__CosmosEndpoint`, `Analytics__Database=analytics`, `Analytics__Container=traffic`, and `Analytics__AdminKey` from Container App secret `analytics-admin-key`. The secure `analyticsAdminKey` deployment parameter defaults to `newGuid()`, rotating on each deployment. It is never output by Bicep or logged by the pipeline. A resource administrator with Container Apps secret-list permission can retrieve it in a private terminal using `az containerapp secret list --resource-group <rg> --name <app> --show-values --query "[?name=='analytics-admin-key'].value | [0]" -o tsv`. Do not run that command in CI or paste the result into logs. Supply the secure parameter explicitly only when key continuity is required. No additional Key Vault access or requests are introduced.

## Official references

- [Free Tier allowances and account restrictions](https://learn.microsoft.com/en-us/azure/cosmos-db/free-tier)
- [Account throughput limit and storage-driven exception](https://learn.microsoft.com/en-us/azure/cosmos-db/limit-total-account-throughput)
- [Periodic backup retention and two free copies](https://learn.microsoft.com/en-us/azure/cosmos-db/periodic-backup-modify-interval-retention)
- [Cosmos data plane roles and database scopes](https://learn.microsoft.com/en-us/azure/cosmos-db/how-to-connect-role-based-access-control)
