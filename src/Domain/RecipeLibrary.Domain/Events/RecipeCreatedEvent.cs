using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Domain.Events;

/// <summary>
/// Event raised when a new recipe is created.
/// </summary>
public sealed record RecipeCreatedEvent(Recipe Recipe);

