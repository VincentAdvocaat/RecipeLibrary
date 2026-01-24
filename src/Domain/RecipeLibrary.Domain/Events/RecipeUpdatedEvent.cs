using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Domain.Events;

/// <summary>
/// Event raised when an existing recipe is updated.
/// </summary>
public sealed record RecipeUpdatedEvent(Recipe Recipe);

