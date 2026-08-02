using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Domain.Entities;

/// <summary>Audit row for a moderation decision (text or image).</summary>
public sealed class ContentModerationEvent
{
    public Guid Id { get; set; }

    /// <summary>Optional: set when the recipe already exists or was just created.</summary>
    public Guid? RecipeId { get; set; }

    /// <summary>
    /// Optional subject key for pre-recipe decisions (e.g. uploaded image URL)
    /// so create/update can attach image NeedsReview to the recipe.
    /// </summary>
    public string? SubjectKey { get; set; }

    public ContentModerationKind Kind { get; set; }

    public ModerationStatus Decision { get; set; }

    /// <summary>Compact category/severity summary for ops (not shown to end users).</summary>
    public string? CategoriesSummary { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
