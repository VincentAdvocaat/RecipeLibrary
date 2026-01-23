namespace RecipeLibrary.Domain.Events;

using RecipeLibrary.Domain.Entities;

/// <summary>
/// Event raised when a new recipe is created.
/// </summary>
public sealed record RecipeCreatedEvent(Recipe Recipe);

/// <summary>
/// Event raised when an existing recipe is updated.
/// </summary>
public sealed record RecipeUpdatedEvent(Recipe Recipe);

