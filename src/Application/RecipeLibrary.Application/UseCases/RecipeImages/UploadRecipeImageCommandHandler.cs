using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Validators;

namespace RecipeLibrary.Application.UseCases.RecipeImages;

public sealed class UploadRecipeImageCommandHandler(IRecipeFileStorage storage, ICurrentUser currentUser)
    : ICommandHandler<UploadRecipeImageCommand, UploadRecipeImageResult>
{
    public async Task<UploadRecipeImageResult> HandleAsync(UploadRecipeImageCommand command, CancellationToken ct = default)
    {
        var ownerUserId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Authentication is required.");

        UploadRecipeImageCommandValidator.ValidateAndThrow(command);

        var url = await storage.SaveAsync(
            command.Content,
            command.FileName,
            command.ContentType,
            ownerUserId,
            ct);
        return new UploadRecipeImageResult(url);
    }
}
