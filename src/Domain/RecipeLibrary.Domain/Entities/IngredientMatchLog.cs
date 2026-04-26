namespace RecipeLibrary.Domain.Entities;

public sealed class IngredientMatchLog
{
    public Guid Id { get; set; }

    public string Input { get; set; } = string.Empty;

    public string NormalizedInput { get; set; } = string.Empty;

    public Guid? MatchedIngredientId { get; set; }

    public string MatchType { get; set; } = string.Empty;

    public decimal? Confidence { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public CanonicalIngredient? MatchedIngredient { get; set; }
}
