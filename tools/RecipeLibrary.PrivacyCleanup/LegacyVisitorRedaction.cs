using System.Globalization;
using System.Text.Json.Nodes;

namespace RecipeLibrary.PrivacyCleanup;

public static class LegacyVisitorRedaction
{
    public static int Count(JsonObject document) => new[] { "events", "Events" }
        .SelectMany(name => (document[name] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
        .SelectMany(item => item.Where(p => p.Key.Equals("visitor", StringComparison.OrdinalIgnoreCase)))
        .Count(p => p.Value is { } value && value.ToJsonString() != "\"\"");

    // A maintenance write must not restart the item's retention clock. Leave a
    // full minute of margin for the bounded write request and clock differences.
    public static (JsonObject Document, int Fields)? Prepare(JsonObject source, DateTimeOffset now)
    {
        var copy = (JsonObject)source.DeepClone();
        var changed = 0;
        foreach (var eventsName in new[] { "events", "Events" })
            if (copy[eventsName] is JsonArray events)
                foreach (var item in events.OfType<JsonObject>())
                    foreach (var field in item.Select(p => p.Key).Where(k => k.Equals("visitor", StringComparison.OrdinalIgnoreCase)).ToArray())
                        if (item[field] is { } value && value.ToJsonString() != "\"\"")
                        {
                            item[field] = "";
                            changed++;
                        }
        if (changed == 0) return null;
        var hour = DateTimeOffset.Parse(copy["hour"]!.GetValue<string>(), CultureInfo.InvariantCulture);
        var expiry = hour.AddDays(31);
        if (copy["_ts"] is { } timestamp && copy["ttl"] is { } ttl && ttl.GetValue<int>() > 0)
        {
            var original = DateTimeOffset.FromUnixTimeSeconds(timestamp.GetValue<long>()).AddSeconds(ttl.GetValue<int>());
            if (original < expiry) expiry = original;
        }
        var remaining = (int)Math.Floor((expiry - now).TotalSeconds) - 60;
        if (remaining < 1) return null; // Already at its normal expiration, not renewed by maintenance.
        copy["ttl"] = remaining;
        foreach (var system in new[] { "_rid", "_self", "_etag", "_attachments", "_ts" }) copy.Remove(system);
        return (copy, changed);
    }
}
