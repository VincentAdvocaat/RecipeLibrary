using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.UseCases.Recipes;

public sealed class SharedRecipeService(
    IRecipeShareRepository shares,
    IRecipeRepository recipes,
    IRecipeFileStorage storage,
    ICurrentUser currentUser,
    IDemoContext demo,
    IUnitOfWork unitOfWork)
{
    public bool CanSave => !demo.IsDemo && currentUser.IsAuthenticated && currentUser.UserId is not null;

    public async Task<(GetRecipeByIdResult Recipe, bool HasStoredImage)?> GetAsync(Guid key, CancellationToken ct)
    {
        var recipe = await shares.GetSharedRecipeAsync(key, ct);
        return recipe is null ? null : (
            GetRecipeByIdQueryHandler.MapToDetailResult(recipe, userId: null),
            RecipeImageAccess.GetStoredImageKey(recipe.ImageUrl) is not null);
    }

    public async Task<GetRecipeImageResult?> GetImageAsync(Guid key, CancellationToken ct)
    {
        var url = await shares.GetSharedImageUrlAsync(key, ct);
        if (RecipeImageAccess.GetStoredImageKey(url) is not { } fileName)
        {
            return null;
        }

        var image = await storage.OpenAsync(fileName, ct);
        return image is { } file ? new GetRecipeImageResult(file.Stream, file.ContentType) : null;
    }

    public async Task<Guid?> SaveCopyAsync(Guid key, CancellationToken ct)
    {
        if (!CanSave || currentUser.UserId is not { } userId)
        {
            throw new UnauthorizedAccessException("An account is required to save a recipe copy.");
        }

        Guid? recipeId = null;
        await unitOfWork.ExecuteInSerializableTransactionAsync(async transactionCt =>
        {
            var source = await shares.GetSharedRecipeAsync(key, transactionCt);
            if (source is null)
            {
                recipeId = null;
                return;
            }

            recipeId = await shares.FindSavedCopyAsync(userId, source.Id, transactionCt);
            if (recipeId is not null)
            {
                return;
            }

            var copy = source.CopyFor(userId);
            copy.SharedFromRecipeId = source.Id;
            await recipes.AddAsync(copy, transactionCt);
            recipeId = copy.Id;
        }, ct);

        return recipeId;
    }
}
