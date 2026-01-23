namespace RecipeLibrary.Application.Contracts;

/// <summary>
/// High-level application service for working with recipes.
/// The Web layer depends on this contract instead of concrete handlers/implementations.
/// </summary>
public interface IRecipeService
{
    Task<Guid> CreateAsync(CreateRecipeRequest request, CancellationToken ct = default);
}

