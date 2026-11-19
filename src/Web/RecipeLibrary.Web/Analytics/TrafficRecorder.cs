using System.Threading.Channels;

namespace RecipeLibrary.Web.Analytics;

public sealed class TrafficRecorder(IConfiguration configuration)
{
    private readonly Channel<TrafficEvent> channel = Channel.CreateBounded<TrafficEvent>(new BoundedChannelOptions(512)
    { FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = false });
    private long dropped;
    private int buffered;
    public long Dropped => Interlocked.Read(ref dropped);
    public int Buffered => Volatile.Read(ref buffered);
    internal bool TryRead(out TrafficEvent item)
    {
        if (!channel.Reader.TryRead(out item!)) return false;
        Interlocked.Decrement(ref buffered); return true;
    }
    internal void Drop(long count) => Interlocked.Add(ref dropped, count);
    private void Enqueue(TrafficEvent item)
    {
        if (System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(item).Length > 2048)
            item = item with { Path = TrafficPrivacy.Bound(item.Path, 64), Host = TrafficPrivacy.Bound(item.Host, 64), Referrer = TrafficPrivacy.Bound(item.Referrer, 64), RequestId = TrafficPrivacy.Bound(item.RequestId, 32) };
        Interlocked.Increment(ref buffered);
        if (!channel.Writer.TryWrite(item)) { Interlocked.Decrement(ref buffered); Interlocked.Increment(ref dropped); }
    }
    public void RecordRequest(HttpContext context)
    {
        if (TrafficPrivacy.Excluded(context.Request.Path)) return;
        RecordEvent("request", context);
    }
    public void RecordEvent(string kind, HttpContext context, string? page = null, string? referrer = null)
    {
        if (!TrafficHour.Kinds.Contains(kind)) return;
        var now = DateTimeOffset.UtcNow;
        var (browser, bot) = TrafficPrivacy.Classify(context.Request.Headers.UserAgent);
        Enqueue(new(now, kind, TrafficPrivacy.Path(page ?? context.Request.Path.Value),
            TrafficPrivacy.Origin($"{context.Request.Scheme}://{context.Request.Host}"), context.Response.StatusCode,
            TrafficPrivacy.Origin(referrer ?? context.Request.Headers.Referer.ToString()), browser, bot,
            TrafficPrivacy.Visitor(context.Connection.RemoteIpAddress?.ToString(), configuration["Analytics:AdminKey"] ?? "", now),
            TrafficPrivacy.Bound(context.TraceIdentifier, 64), Method: TrafficPrivacy.Bound(context.Request.Method, 8)));
    }
    public void RecordSqlConnection(string kind, HttpContext? context, string reason)
    {
        if (kind is not ("database-connection-open" or "database-connection-failure")) return;
        Enqueue(new(DateTimeOffset.UtcNow, kind, Path: TrafficPrivacy.Path(context?.Request.Path.Value ?? "/background"),
            RequestId: TrafficPrivacy.Bound(context?.TraceIdentifier, 64), Reason: reason));
    }
    public void RecordLifecycle(string kind, string? reason = null, string? correlationId = null)
    {
        if (!TrafficHour.Kinds.Contains(kind)) return;
        // Reasons are fixed labels, never exception messages or connection strings.
        var safeReason = reason is "manual" or "user-start" or "userstart" or "startup" or "idle" or "success" or "failure" or "timeout" or "probe" ? reason : "other";
        Enqueue(new(DateTimeOffset.UtcNow, kind, RequestId: TrafficPrivacy.Bound(correlationId, 64), Reason: safeReason));
    }
}

public sealed class TrafficWriter(TrafficRecorder recorder, ITrafficStore store) : BackgroundService
{
    private readonly string writer = Guid.NewGuid().ToString("N");
    private long sequence;
    private readonly SemaphoreSlim gate = new(1);
    public bool Available { get; private set; } = !store.Configured;
    public DateTimeOffset? LastSuccess { get; private set; }
    public string? Error { get; private set; }
    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var batch = new Dictionary<string, TrafficHour>();
            while (recorder.TryRead(out var item))
            {
                var hour = TrafficHour.Create(item.At);
                if (!batch.TryGetValue(hour.id, out var existing)) batch[hour.id] = existing = hour;
                existing.Record(item);
                // One flush never drains an endlessly replenished channel.
                if (batch.Values.Sum(h => h.Counts.Values.Sum()) >= 512) break;
            }
            foreach (var delta in batch.Values)
            {
                var number = ++sequence;
                var saved = false;
                for (var attempt = 0; attempt < 3 && !saved && !cancellationToken.IsCancellationRequested; attempt++)
                {
                    try
                    {
                        await store.MergeAsync(delta, writer, number, cancellationToken);
                        Available = true; Error = null; LastSuccess = DateTimeOffset.UtcNow; saved = true;
                    }
                    catch (Exception) when (!cancellationToken.IsCancellationRequested)
                    {
                        Available = false; Error = "Analytics storage unavailable";
                        if (attempt < 2) await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), cancellationToken);
                    }
                }
                if (!saved) recorder.Drop(delta.Counts.Values.Sum());
            }
        }
        finally { gate.Release(); }
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try { while (await timer.WaitForNextTickAsync(stoppingToken)) await FlushAsync(stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        try { await FlushAsync(cancellationToken); } catch (OperationCanceledException) { }
    }
}
