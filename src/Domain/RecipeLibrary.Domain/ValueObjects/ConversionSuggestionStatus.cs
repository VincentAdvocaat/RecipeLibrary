namespace RecipeLibrary.Domain.ValueObjects;

/// <summary>
/// Lifecycle status for an AI-proposed conversion candidate.
/// </summary>
public enum ConversionSuggestionStatus
{
    Pending = 1,
    Accepted = 2,
    Rejected = 3,
}
