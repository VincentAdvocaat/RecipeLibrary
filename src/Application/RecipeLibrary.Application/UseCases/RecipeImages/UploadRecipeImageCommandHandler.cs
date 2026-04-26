using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Validators;

namespace RecipeLibrary.Application.UseCases.RecipeImages;

public sealed class UploadRecipeImageCommandHandler(IRecipeFileStorage storage)
    : ICommandHandler<UploadRecipeImageCommand, UploadRecipeImageResult>
{
    public async Task<UploadRecipeImageResult> HandleAsync(UploadRecipeImageCommand command, CancellationToken ct = default)
    {
        UploadRecipeImageCommandValidator.ValidateAndThrow(command);

        var url = await storage.SaveAsync(command.Content, command.FileName, command.ContentType, ct);
        return new UploadRecipeImageResult(url);
    }
}
