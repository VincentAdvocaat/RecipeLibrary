using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace RecipeLibrary.Web.Analytics;

public sealed record TrafficEvent(DateTimeOffset At, string Kind, string Path = "", string Host = "", int Status = 0,
    string Referrer = "", string Browser = "", string Bot = "", string Visitor = "", string RequestId = "", string Reason = "", string Method = "", string AttemptId = "");

public static partial class TrafficPrivacy
{
    public static string Bound(string? value, int length) => new((value ?? "").Where(c => !char.IsControl(c)).Take(length).ToArray());
    public static string Path(string? value)
    {
        var path = Bound(value, 1024).Split('?', '#')[0];
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri)) path = uri.AbsolutePath;
        // Decode twice to catch ordinary and double-encoded personal identifiers.
        for (var i = 0; i < 2; i++) path = Uri.UnescapeDataString(path);
        path = path.Split('?', '#')[0];
        path = GuidPattern().Replace(path, "/:id");
        // Segments resembling email addresses or opaque tokens are never stored.
        path = string.Join('/', path.Split('/').Select(s => s.Contains('@') || s.Contains('%') || s.Length > 48 ? ":id" : s));
        return Bound(path.StartsWith('/') ? path : "/", 128);
    }
    public static string Origin(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == "https" || uri.Scheme == "http") && string.IsNullOrEmpty(uri.UserInfo)
        ? Bound(uri.GetLeftPart(UriPartial.Authority), 128) : "";
    public static string Visitor(string? ip, string secret, DateTimeOffset now) => secret.Length < 32 || string.IsNullOrEmpty(ip) ? "" :
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(now.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "|" + ip)))[..12].ToLowerInvariant();
    public static (string Browser, string Bot) Classify(string? raw)
    {
        var ua = Bound(raw, 180).ToLowerInvariant();
        var family = new[] { "googlebot", "bingbot", "ahrefsbot", "semrushbot", "petalbot", "uptimerobot", "curl", "python", "wget" }.FirstOrDefault(ua.Contains);
        var bot = family is not null ? family + "-inferred" : ua.Contains("bot") || ua.Contains("crawler") || ua.Contains("spider") ? "bot-inferred" :
            ua.Contains("curl") || ua.Contains("python") || ua.Contains("wget") ? "automation-inferred" : "unknown";
        var browser = ua.Contains("edg/") ? "Edge" : ua.Contains("firefox/") ? "Firefox" : ua.Contains("chrome/") ? "Chrome" : ua.Contains("safari/") ? "Safari" : "Other";
        return (browser, bot);
    }
    public static bool Excluded(string? raw)
    {
        var p = (raw ?? "").ToLowerInvariant();
        return p.StartsWith("/api/analytics") || p.StartsWith("/analytics") || p.StartsWith("/health") || p.StartsWith("/_") ||
            new[] { ".js", ".css", ".png", ".jpg", ".jpeg", ".svg", ".ico", ".woff", ".woff2", ".map" }.Any(p.EndsWith);
    }
    [GeneratedRegex(@"/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}(?=/|$)")]
    private static partial Regex GuidPattern();
}

public sealed class TrafficHour
{
    public string id { get; set; } = "";
    public string day { get; set; } = "";
    public DateTimeOffset Hour { get; set; }
    public int ttl { get; set; }
    public Dictionary<string, long> Counts { get; set; } = new();
    public Dictionary<string, long> Paths { get; set; } = new();
    public Dictionary<string, long> Referrers { get; set; } = new();
    public Dictionary<string, long> Bots { get; set; } = new();
    public List<TrafficEvent> Events { get; set; } = new();
    public long DroppedDetails { get; set; }
    public Dictionary<string, long> BatchSequences { get; set; } = new();
    public static readonly string[] Kinds = ["request", "landing", "browser", "open-app", "database-start-requested", "database-start-success", "database-start-failure", "database-probe", "database-probe-failure", "database-connection-open", "database-connection-failure"];
    public static TrafficHour Create(DateTimeOffset at)
    {
        var hour = new DateTimeOffset(at.UtcDateTime.Date.AddHours(at.UtcDateTime.Hour), TimeSpan.Zero);
        return new() { id = hour.ToString("yyyyMMddHH"), day = hour.ToString("yyyy-MM-dd"), Hour = hour };
    }
    public static long Add(long a, long b) => Math.Min(1_000_000_000, a + b);
    private static void Increment(Dictionary<string, long> dict, string key, long count)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (!dict.ContainsKey(key) && dict.Count >= 99) key = "(other)";
        dict[key] = Add(dict.GetValueOrDefault(key), count);
    }
    public void Record(TrafficEvent item)
    {
        if (!Kinds.Contains(item.Kind)) return;
        Increment(Counts, item.Kind, 1);
        Increment(Paths, item.Path, 1); Increment(Referrers, item.Referrer, 1); Increment(Bots, item.Bot, 1);
        AddDetail(item);
    }
    private void AddDetail(TrafficEvent item)
    {
        if (Events.Count < 200) { Events.Add(item); return; }
        if (item.Kind.StartsWith("database-", StringComparison.Ordinal))
        {
            var replace = Events.FindIndex(e => !e.Kind.StartsWith("database-", StringComparison.Ordinal));
            if (replace >= 0) Events[replace] = item;
        }
        DroppedDetails = Add(DroppedDetails, 1);
    }
    // A writer has only one pending batch. Sequence marks make ambiguous network retries idempotent.
    public bool Merge(TrafficHour delta, string writer, long sequence, DateTimeOffset now)
    {
        if (Hour != delta.Hour || Hour.AddDays(31) <= now) return false;
        if (BatchSequences.GetValueOrDefault(writer) >= sequence) return false;
        if (!BatchSequences.ContainsKey(writer) && BatchSequences.Count >= 100) throw new InvalidOperationException("Hourly writer limit reached.");
        foreach (var pair in delta.Counts.Where(p => Kinds.Contains(p.Key))) Increment(Counts, pair.Key, pair.Value);
        foreach (var pair in delta.Paths) Increment(Paths, pair.Key, pair.Value);
        foreach (var pair in delta.Referrers) Increment(Referrers, pair.Key, pair.Value);
        foreach (var pair in delta.Bots) Increment(Bots, pair.Key, pair.Value);
        foreach (var item in delta.Events) AddDetail(item);
        DroppedDetails = Add(DroppedDetails, delta.DroppedDetails);
        BatchSequences[writer] = sequence;
        ttl = Math.Max(1, (int)Math.Floor((Hour.AddDays(31) - now).TotalSeconds));
        return true;
    }
}
