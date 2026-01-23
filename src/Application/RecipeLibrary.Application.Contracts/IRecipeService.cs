using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.Contracts;

/// <summary>
/// High-level application service for working with recipes.
/// The Web layer depends on this contract instead of concrete handlers/implementations.
/// </summary>
public interface IRecipeService
{
    // Methods such as CreateRecipeAsync, UpdateRecipeAsync, etc. will be defined later.
}

