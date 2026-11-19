using Azure.Identity;
using Microsoft.Azure.Cosmos;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using RecipeLibrary.PrivacyCleanup;

// Deliberately fixed to this application's analytics container. Never opens SQL,
// creates resources, grants roles, reads account keys or accepts an arbitrary target.
const string endpoint = "https://cosmos-analytics-txv3wjhyrukva.documents.azure.com:443/";
if (args.Any(a => a is not ("--apply" or "--dry-run")) || args.Contains("--apply") && args.Contains("--dry-run"))
    throw new ArgumentException("Use --dry-run (default) or --apply.");
var apply = args.Contains("--apply");
using var client = new CosmosClient(endpoint, new DefaultAzureCredential(new DefaultAzureCredentialOptions { ExcludeInteractiveBrowserCredential = true }),
    new CosmosClientOptions { ConnectionMode = ConnectionMode.Gateway, LimitToEndpoint = true,
        UseSystemTextJsonSerializerWithOptions = new(JsonSerializerDefaults.Web), RequestTimeout = TimeSpan.FromSeconds(10), MaxRetryAttemptsOnRateLimitedRequests = 0 });
var container = client.GetContainer("analytics", "traffic");
var scanned = 0; var affected = 0; var redacted = 0; var found = 0;
try
{
    using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    using var iterator = container.GetItemQueryIterator<JsonObject>("SELECT * FROM c", requestOptions: new() { MaxItemCount = 1, MaxConcurrency = 1 });
    while (iterator.HasMoreResults)
        foreach (var item in await iterator.ReadNextAsync(deadline.Token))
        {
            if (++scanned > 745) throw new InvalidOperationException("Unexpected item count; bounded maintenance stopped.");
            found += LegacyVisitorRedaction.Count(item);
            var id = item["id"]!.GetValue<string>();
            var day = item["day"]!.GetValue<string>();
            var current = item;
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var plan = LegacyVisitorRedaction.Prepare(current, DateTimeOffset.UtcNow);
                if (plan is null) break;
                if (!apply) { affected++; redacted += plan.Value.Fields; break; }
                using var writeDeadline = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
                writeDeadline.CancelAfter(TimeSpan.FromSeconds(20));
                try
                {
                    await container.ReplaceItemAsync(plan.Value.Document, id, new PartitionKey(day),
                        new ItemRequestOptions { IfMatchEtag = current["_etag"]!.GetValue<string>() }, writeDeadline.Token);
                    affected++; redacted += plan.Value.Fields; break;
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed && attempt < 4)
                {
                    current = (await container.ReadItemAsync<JsonObject>(id, new PartitionKey(day), cancellationToken: deadline.Token)).Resource;
                }
            }
        }
    Console.WriteLine(JsonSerializer.Serialize(new { mode = apply ? "apply" : "dry-run", scanned, affected, visitorFieldsFound = found, visitorFields = redacted }));
}
catch (Exception ex)
{
    // Do not dump documents, credential diagnostics, headers or pseudonyms.
    Console.Error.WriteLine("Privacy maintenance failed: " + ex.GetType().Name);
    Environment.ExitCode = 1;
}
