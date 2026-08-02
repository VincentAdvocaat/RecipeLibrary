using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.ContentModeration;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Validators;

namespace RecipeLibrary.Application.UseCases.RecipeImages;

public sealed class UploadRecipeImageCommandHandler(
    IRecipeFileStorage storage,
    RecipeContentModerationService contentModeration,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UploadRecipeImageCommand, UploadRecipeImageResult>
{
    public async Task<UploadRecipeImageResult> HandleAsync(UploadRecipeImageCommand command, CancellationToken ct = default)
    {
        UploadRecipeImageCommandValidator.ValidateAndThrow(command);

        await using var buffer = new MemoryStream();
        await command.Content.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        try
        {
            await contentModeration.EnsureImageAllowedAsync(buffer, command.ContentType, ct);
        }
        catch (ContentRejectedException)
        {
            await unitOfWork.SaveChangesAsync(ct);
            throw;
        }

        await unitOfWork.SaveChangesAsync(ct);

        buffer.Position = 0;
        var url = await storage.SaveAsync(buffer, command.FileName, command.ContentType, ct);
        return new UploadRecipeImageResult(url);
    }
}
