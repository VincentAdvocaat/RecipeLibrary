namespace RecipeLibrary.Application.Contracts;

public sealed record SetRecipeRatingCommand(Guid RecipeId, int Stars) : ICommand<SetRecipeRatingResult>;

public sealed record SetRecipeRatingResult(bool Saved);
