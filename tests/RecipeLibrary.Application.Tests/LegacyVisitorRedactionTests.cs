using System.Text.Json.Nodes;
using RecipeLibrary.PrivacyCleanup;
using Xunit;

public sealed class LegacyVisitorRedactionTests
{
    [Fact]
    public void Redaction_preserves_statistics_and_does_not_renew_retention()
    {
        var now = DateTimeOffset.UtcNow;
        var original = new JsonObject { ["id"]="hour", ["day"]="2026-11-19", ["hour"]=now.AddDays(-1).ToString("O"),
            ["_ts"]=now.AddMinutes(-10).ToUnixTimeSeconds(), ["ttl"]=1200, ["_etag"]="old",
            ["counts"]=new JsonObject { ["request"]=42 }, ["events"]=new JsonArray(new JsonObject { ["visitor"]="legacy-secret",["requestId"]="keep-request" }) };
        var plan = LegacyVisitorRedaction.Prepare(original, now)!.Value;
        Assert.Equal(1, plan.Fields);
        Assert.Equal(0, LegacyVisitorRedaction.Count(plan.Document));
        Assert.Equal(1, LegacyVisitorRedaction.Count(original)); // Caller data not mutated before ETag write.
        Assert.Equal(42, plan.Document["counts"]!["request"]!.GetValue<int>());
        Assert.Equal("keep-request", plan.Document["events"]![0]!["requestId"]!.GetValue<string>());
        Assert.InRange(plan.Document["ttl"]!.GetValue<int>(), 538, 540);
        Assert.Null(plan.Document["_etag"]);
    }
    [Fact]
    public void Fixed_hour_expiry_bounds_legacy_ttl_and_empty_data_is_not_written()
    {
        var now=DateTimeOffset.UtcNow;
        var doc=new JsonObject { ["hour"]=now.AddDays(-30).ToString("O"), ["_ts"]=now.ToUnixTimeSeconds(), ["ttl"]=2678400,
            ["Events"]=new JsonArray(new JsonObject { ["Visitor"]="old" }) };
        var plan=LegacyVisitorRedaction.Prepare(doc,now)!.Value;
        Assert.Equal(86340, plan.Document["ttl"]!.GetValue<int>());
        Assert.Null(LegacyVisitorRedaction.Prepare(plan.Document,now));
        doc["hour"]=now.AddDays(-32).ToString("O");
        Assert.Null(LegacyVisitorRedaction.Prepare(doc,now));
        Assert.Equal(1, LegacyVisitorRedaction.Count(doc)); // Still reported as found, not falsely claimed clean.
    }
}
