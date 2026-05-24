using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.Validators;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.UseCases.Recipes;

public sealed class CreateRecipeCommandHandler(
    IRecipeRepository recipeRepository,
    IIngredientRepository ingredientRepository,
    IIngredientTextNormalizer normalizer,
    IngredientMatcher matcher,
    IngredientLineResolver lineResolver)
    : ICommandHandler<CreateRecipeCommand, CreateRecipeResult>
{
    public async Task<CreateRecipeResult> HandleAsync(CreateRecipeCommand command, CancellationToken ct = default)
    {
        CreateRecipeCommandValidator.ValidateAndThrow(command);

        var title = (command.Title ?? string.Empty).Trim();
        var recipeId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var category = Enum.IsDefined(typeof(RecipeCategory), command.Category)
            ? (RecipeCategory)command.Category
            : RecipeCategory.Unknown;

        var description = (command.Description ?? string.Empty).Trim();
        var imageUrl = (command.ImageUrl ?? string.Empty).Trim();

        var recipe = new Recipe
        {
            Id = recipeId,
            Title = new RecipeTitle(title),
            Description = string.IsNullOrEmpty(description) ? null : description,
            ImageUrl = string.IsNullOrEmpty(imageUrl) ? null : imageUrl,
            PreparationMinutes = command.PreparationTimeMinutes,
            CookingMinutes = command.CookingTimeMinutes,
            Category = category,
            Difficulty = Difficulty.Unknown,
            Servings = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var builtIngredients = await RecipeIngredientBuilder.BuildAsync(
            recipeId,
            command.Ingredients,
            ingredientRepository,
            normalizer,
            matcher,
            lineResolver,
            ct);
        foreach (var ingredient in builtIngredients)
        {
            recipe.Ingredients.Add(ingredient);
        }

        foreach (var step in RecipeIngredientBuilder.BuildSteps(recipeId, command.InstructionSteps))
        {
            recipe.InstructionSteps.Add(step);
        }

        await recipeRepository.AddAsync(recipe, ct);
        return new CreateRecipeResult(recipeId);
    }
}

