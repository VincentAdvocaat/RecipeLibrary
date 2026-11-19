using System.Net;
using System.Text.Json;
using Azure.Identity;
using Microsoft.Azure.Cosmos;

namespace RecipeLibrary.Web.Analytics;

public interface ITrafficStore
{
    bool Configured { get; }
    Task MergeAsync(TrafficHour delta, string writer, long sequence, CancellationToken cancellationToken);
    Task<IReadOnlyList<TrafficHour>> ReadAsync(DateTimeOffset from, CancellationToken cancellationToken);
}

public sealed class MemoryTrafficStore : ITrafficStore
{
    private readonly Dictionary<string, TrafficHour> hours = new();
    public bool Configured => false;
    public Task MergeAsync(TrafficHour delta, string writer, long sequence, CancellationToken cancellationToken)
    {
        lock (hours)
        {
            foreach (var key in hours.Where(p => p.Value.Hour.AddDays(31) <= DateTimeOffset.UtcNow).Select(p => p.Key).ToArray()) hours.Remove(key);
            if (!hours.TryGetValue(delta.id, out var hour)) hours[delta.id] = hour = TrafficHour.Create(delta.Hour);
            hour.Merge(delta, writer, sequence, DateTimeOffset.UtcNow);
            while (hours.Count > 744) hours.Remove(hours.Keys.Order().First());
        }
        return Task.CompletedTask;
    }
    public Task<IReadOnlyList<TrafficHour>> ReadAsync(DateTimeOffset from, CancellationToken cancellationToken)
    {
        lock (hours)
        {
            var remaining = 200;
            var result = new List<TrafficHour>();
            foreach (var h in hours.Values.Where(h => h.Hour >= TrafficHour.Create(from).Hour && h.Hour.AddDays(31) > DateTimeOffset.UtcNow).OrderByDescending(h => h.Hour))
            {
                var copy = new TrafficHour { id = h.id, day = h.day, Hour = h.Hour, Counts = new(h.Counts), Paths = new(h.Paths),
                    Referrers = new(h.Referrers), Bots = new(h.Bots), DroppedDetails = h.DroppedDetails, Events = h.Events.OrderByDescending(e => e.At).Take(remaining).ToList() };
                remaining -= copy.Events.Count; result.Add(copy);
            }
            return Task.FromResult<IReadOnlyList<TrafficHour>>(result.OrderBy(h => h.Hour).ToArray());
        }
    }
}

public sealed class CosmosTrafficStore : ITrafficStore, IDisposable
{
    private readonly CosmosClient client;
    private readonly Container container;
    public bool Configured => true;
    public CosmosTrafficStore(IConfiguration configuration)
    {
        client = new CosmosClient(configuration["Analytics:CosmosEndpoint"], new DefaultAzureCredential(new DefaultAzureCredentialOptions
        { ManagedIdentityClientId = configuration["AZURE_CLIENT_ID"] }), new CosmosClientOptions
        {
            ConnectionMode = ConnectionMode.Gateway,
            UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web),
            MaxRetryAttemptsOnRateLimitedRequests = 1,
            MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(2),
            RequestTimeout = TimeSpan.FromSeconds(5)
        });
        // Infrastructure provisions the fixed-RU container. The application never creates paid resources.
        container = client.GetContainer("analytics", "traffic");
    }
    public async Task MergeAsync(TrafficHour delta, string writer, long sequence, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            TrafficHour hour;
            string? etag = null;
            try
            {
                var read = await container.ReadItemAsync<TrafficHour>(delta.id, new PartitionKey(delta.day), cancellationToken: cancellationToken);
                hour = read.Resource; etag = read.ETag;
            }
            catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound) { hour = TrafficHour.Create(delta.Hour); }
            if (!hour.Merge(delta, writer, sequence, DateTimeOffset.UtcNow)) return;
            if (JsonSerializer.SerializeToUtf8Bytes(hour, new JsonSerializerOptions(JsonSerializerDefaults.Web)).Length > 1_000_000)
                throw new InvalidOperationException("Analytics hourly storage limit reached.");
            try
            {
                if (etag is null) await container.CreateItemAsync(hour, new PartitionKey(hour.day), cancellationToken: cancellationToken);
                else await container.ReplaceItemAsync(hour, hour.id, new PartitionKey(hour.day), new ItemRequestOptions { IfMatchEtag = etag }, cancellationToken);
                return;
            }
            catch (CosmosException e) when (e.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed && attempt < 3) { }
        }
        throw new InvalidOperationException("Analytics concurrency retry limit reached.");
    }
    public async Task<IReadOnlyList<TrafficHour>> ReadAsync(DateTimeOffset from, CancellationToken cancellationToken)
    {
        var result = new List<TrafficHour>();
        var now = DateTimeOffset.UtcNow;
        from = from < now.AddDays(-31) ? now.AddDays(-31) : from;
        // Partition-scoped queries avoid an unbounded cross-partition scan.
        for (var date = from.UtcDateTime.Date; date <= now.UtcDateTime.Date && result.Count < 745; date = date.AddDays(1))
        {
            using var iterator = container.GetItemQueryIterator<TrafficHour>(new QueryDefinition("SELECT c.id,c.day,c.hour,c.counts,c.paths,c.referrers,c.bots,c.droppedDetails FROM c WHERE c.id >= @from AND c.id <= @to")
                .WithParameter("@from", TrafficHour.Create(from).id).WithParameter("@to", TrafficHour.Create(now).id),
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(date.ToString("yyyy-MM-dd")), MaxItemCount = 24 });
            for (var page = 0; iterator.HasMoreResults && page < 4; page++)
                result.AddRange((await iterator.ReadNextAsync(cancellationToken)).Where(h => h.Hour.AddDays(31) > now).Take(745 - result.Count));
        }
        var remaining = 200;
        for (var date = now.UtcDateTime.Date; date >= from.UtcDateTime.Date && remaining > 0; date = date.AddDays(-1))
        {
            using var details = container.GetItemQueryIterator<TrafficHour>(new QueryDefinition("SELECT c.id,ARRAY_SLICE(c.events,0,@limit) AS events FROM c WHERE c.id >= @from AND c.id <= @to ORDER BY c.id DESC")
                .WithParameter("@limit", remaining).WithParameter("@from", TrafficHour.Create(from).id).WithParameter("@to", TrafficHour.Create(now).id),
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(date.ToString("yyyy-MM-dd")), MaxItemCount = 1 });
            for (var page = 0; details.HasMoreResults && page < 24 && remaining > 0; page++)
            {
                foreach (var detail in await details.ReadNextAsync(cancellationToken))
                {
                    var hour = result.FirstOrDefault(h => h.id == detail.id);
                    if (hour is null) continue;
                    hour.Events = detail.Events.Take(remaining).ToList();
                    remaining -= hour.Events.Count;
                }
            }
        }
        return result.OrderBy(h => h.Hour).ToArray();
    }
    public void Dispose() => client.Dispose();
}
