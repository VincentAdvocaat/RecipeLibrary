namespace RecipeLibrary.Domain.Entities;

/// <summary>User-submitted report against a recipe (for admin review queue).</summary>
public sealed class ContentReport
{
    public Guid Id { get; set; }

    public Guid RecipeId { get; set; }

    public string ReporterUserId { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public bool Handled { get; set; }

    public DateTimeOffset? HandledAt { get; set; }
}
