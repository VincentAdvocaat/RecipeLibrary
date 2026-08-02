namespace RecipeLibrary.Application.Abstractions;

public sealed class ContentModerationOptions
{
    public const string SectionName = "ContentModeration";

    public const string AdminRoleName = "Admin";

    /// <summary>Master switch. Default false — no provider calls when off.</summary>
    public bool Enabled { get; init; }

    /// <summary>Azure AI Content Safety endpoint, e.g. https://{name}.cognitiveservices.azure.com/</summary>
    public string? Endpoint { get; init; }

    /// <summary>API key for Content Safety. Prefer Key Vault / env in deployed environments.</summary>
    public string? ApiKey { get; init; }

    /// <summary>Severity at or above this value blocks persist (default 4).</summary>
    public int BlockSeverityThreshold { get; init; } = 4;

    /// <summary>Severity at or above this (and below block) marks NeedsReview (default 2).</summary>
    public int ReviewSeverityThreshold { get; init; } = 2;

    /// <summary>Emails granted the Admin role at startup (Development and/or when configured).</summary>
    public string[] AdminEmails { get; init; } = [];
}
