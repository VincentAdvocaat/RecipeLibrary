using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.Validators;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.UseCases.Recipes;

public sealed class UpdateRecipeCommandHandler(
    IRecipeRepository recipeRepository,
    IIngredientRepository ingredientRepository,
    IIngredientTextNormalizer normalizer,
    IngredientMatcher matcher,
    IngredientLineResolver lineResolver,
    ICurrentUser currentUser)
    : ICommandHandler<UpdateRecipeCommand, UpdateRecipeResult>
{
    public async Task<UpdateRecipeResult> HandleAsync(UpdateRecipeCommand command, CancellationToken ct = default)
    {
        var ownerUserId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Authentication is required.");

        var createShape = new CreateRecipeCommand
        {
            Title = command.Title,
            Description = command.Description,
            ImageUrl = command.ImageUrl,
            PreparationTimeMinutes = command.PreparationTimeMinutes,
            CookingTimeMinutes = command.CookingTimeMinutes,
            Category = command.Category,
            Servings = command.Servings,
            Difficulty = command.Difficulty,
            Ingredients = command.Ingredients,
            InstructionSteps = command.InstructionSteps,
        };
        CreateRecipeCommandValidator.ValidateAndThrow(createShape);

        var existing = await recipeRepository.GetByIdAsync(ownerUserId, command.RecipeId, ct)
            ?? throw new InvalidOperationException($"Recipe '{command.RecipeId}' was not found.");

        var title = (command.Title ?? string.Empty).Trim();
        var category = Enum.IsDefined(typeof(RecipeCategory), command.Category)
            ? (RecipeCategory)command.Category
            : RecipeCategory.Unknown;
        var difficulty = Enum.IsDefined(typeof(Difficulty), command.Difficulty)
            ? (Difficulty)command.Difficulty
            : Difficulty.Unknown;
        var description = (command.Description ?? string.Empty).Trim();
        var imageUrl = (command.ImageUrl ?? string.Empty).Trim();

        var builtIngredients = await RecipeIngredientBuilder.BuildAsync(
            existing.Id,
            command.Ingredients,
            ingredientRepository,
            normalizer,
            matcher,
            lineResolver,
            ct);

        var builtSteps = RecipeIngredientBuilder.BuildSteps(existing.Id, command.InstructionSteps);

        var recipe = new Recipe
        {
            Id = existing.Id,
            OwnerUserId = ownerUserId,
            Title = new RecipeTitle(title),
            Description = string.IsNullOrEmpty(description) ? null : description,
            ImageUrl = string.IsNullOrEmpty(imageUrl) ? null : imageUrl,
            PreparationMinutes = command.PreparationTimeMinutes,
            CookingMinutes = command.CookingTimeMinutes,
            Category = category,
            Difficulty = difficulty,
            Servings = command.Servings,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow,
            Ingredients = builtIngredients.ToList(),
            InstructionSteps = builtSteps.ToList(),
        };

        await recipeRepository.UpdateAsync(ownerUserId, recipe, ct);
        return new UpdateRecipeResult(recipe.Id);
    }
}
