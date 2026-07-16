using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.RecipeImport;

namespace RecipeLibrary.Application.UseCases.RecipeImport;

public sealed class CreateRecipeImportImageSessionCommandHandler(
    IRecipeImportStagingStore stagingStore,
    IOptions<RecipeImportOptions> options)
    : ICommandHandler<CreateRecipeImportImageSessionCommand, CreateRecipeImportImageSessionResult>
{
    public async Task<CreateRecipeImportImageSessionResult> HandleAsync(
        CreateRecipeImportImageSessionCommand command,
        CancellationToken ct = default)
    {
        var ttlMinutes = Math.Max(1, options.Value.Ocr.StagingTtlMinutes);
        var session = await stagingStore.CreateSessionAsync(
            command.OwnerKey ?? string.Empty,
            TimeSpan.FromMinutes(ttlMinutes),
            ct);

        return new CreateRecipeImportImageSessionResult
        {
            SessionId = session.SessionId,
            ExpiresUtc = session.ExpiresUtc,
        };
    }
}

public sealed class AddRecipeImportImageCommandHandler(
    IRecipeImportStagingStore stagingStore,
    IOptions<RecipeImportOptions> options)
    : ICommandHandler<AddRecipeImportImageCommand, AddRecipeImportImageResult>
{
    public async Task<AddRecipeImportImageResult> HandleAsync(
        AddRecipeImportImageCommand command,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.SessionId))
        {
            throw new ArgumentException("Session id is required.");
        }

        if (command.ImageBytes is null || command.ImageBytes.Length == 0)
        {
            throw new ArgumentException("Image is required.");
        }

        var maxBytes = options.Value.Ocr.MaxImageBytes;
        if (command.ImageBytes.Length > maxBytes)
        {
            throw new ArgumentException($"Image exceeds maximum size of {maxBytes} bytes.");
        }

        var contentType = ImportRecipeFromImageQueryHandler.ResolveContentType(command.ContentType, command.FileName);
        await using var stream = new MemoryStream(command.ImageBytes, writable: false);
        var image = await stagingStore.AddImageAsync(
            command.SessionId.Trim(),
            command.OwnerKey ?? string.Empty,
            stream,
            command.FileName,
            contentType,
            options.Value.Ocr.MaxImagesPerSession,
            ct);

        var session = await stagingStore.GetSessionAsync(command.SessionId.Trim(), ct)
            ?? throw new InvalidOperationException("Import session was not found.");

        return new AddRecipeImportImageResult
        {
            ImageId = image.ImageId,
            Order = image.Order,
            FileName = image.FileName,
            ImageCount = session.Images.Count,
        };
    }
}

public sealed class RemoveRecipeImportImageCommandHandler(IRecipeImportStagingStore stagingStore)
    : ICommandHandler<RemoveRecipeImportImageCommand, RemoveRecipeImportImageResult>
{
    public async Task<RemoveRecipeImportImageResult> HandleAsync(
        RemoveRecipeImportImageCommand command,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.SessionId) || string.IsNullOrWhiteSpace(command.ImageId))
        {
            throw new ArgumentException("Session id and image id are required.");
        }

        await stagingStore.RemoveImageAsync(
            command.SessionId.Trim(),
            command.OwnerKey ?? string.Empty,
            command.ImageId.Trim(),
            ct);

        var session = await stagingStore.GetSessionAsync(command.SessionId.Trim(), ct);
        return new RemoveRecipeImportImageResult { ImageCount = session?.Images.Count ?? 0 };
    }
}

public sealed class DeleteRecipeImportImageSessionCommandHandler(IRecipeImportStagingStore stagingStore)
    : ICommandHandler<DeleteRecipeImportImageSessionCommand, DeleteRecipeImportImageSessionResult>
{
    public async Task<DeleteRecipeImportImageSessionResult> HandleAsync(
        DeleteRecipeImportImageSessionCommand command,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.SessionId))
        {
            throw new ArgumentException("Session id is required.");
        }

        var sessionId = command.SessionId.Trim();
        var session = await stagingStore.GetSessionAsync(sessionId, ct);
        if (session is null)
        {
            return new DeleteRecipeImportImageSessionResult { Deleted = false };
        }

        RecipeImportStagingAccess.EnsureOwner(session, command.OwnerKey);
        await stagingStore.DeleteSessionAsync(sessionId, ct);
        return new DeleteRecipeImportImageSessionResult { Deleted = true };
    }
}

public sealed class ProcessRecipeImportImageSessionQueryHandler(
    IRecipeImportStagingStore stagingStore,
    IRecipeImageTextExtractor imageTextExtractor,
    RecipeImportService recipeImportService,
    ILogger<ProcessRecipeImportImageSessionQueryHandler> logger)
    : IQueryHandler<ProcessRecipeImportImageSessionQuery, ImportRecipeResult>
{
    public async Task<ImportRecipeResult> HandleAsync(
        ProcessRecipeImportImageSessionQuery query,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query.SessionId))
        {
            throw new ArgumentException("Session id is required.");
        }

        var sessionId = query.SessionId.Trim();
        var session = await stagingStore.GetSessionAsync(sessionId, ct)
            ?? throw new InvalidOperationException("Import session was not found or has expired.");

        RecipeImportStagingAccess.EnsureOwner(session, query.OwnerKey);
        RecipeImportStagingAccess.EnsureNotExpired(session);

        if (session.Images.Count == 0)
        {
            throw new ArgumentException("Add at least one image before processing.");
        }

        try
        {
            var language = ImportRecipeFromImageQueryHandler.NormalizeLanguage(query.Language);
            var textParts = new List<string>();

            foreach (var image in session.Images.OrderBy(x => x.Order))
            {
                if (await stagingStore.OpenImageAsync(sessionId, query.OwnerKey ?? string.Empty, image.ImageId, ct)
                    is not { } openedImage)
                {
                    throw new InvalidOperationException($"Staged image '{image.ImageId}' was not found.");
                }

                await using var stream = openedImage.Stream;
                var text = await imageTextExtractor.ExtractTextAsync(stream, language, ct);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    textParts.Add(text.Trim());
                }
            }

            if (textParts.Count == 0)
            {
                throw new InvalidOperationException("No text could be extracted from the images.");
            }

            return await recipeImportService.ImportContentAsync(
                new ImportRecipeContentQuery
                {
                    Content = string.Join("\n\n", textParts),
                    ContentKind = ImportContentKind.PlainText,
                },
                ct);
        }
        finally
        {
            try
            {
                await stagingStore.DeleteSessionAsync(sessionId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to delete recipe import staging session {SessionId} after processing; leftovers rely on TTL cleanup.",
                    sessionId);
            }
        }
    }
}

internal static class RecipeImportStagingAccess
{
    public static void EnsureOwner(RecipeImportStagingSession session, string? ownerKey)
    {
        var expected = session.OwnerKey ?? string.Empty;
        if (expected.Length == 0)
        {
            return;
        }

        if (!string.Equals(expected, ownerKey ?? string.Empty, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Import session does not belong to the current user.");
        }
    }

    public static void EnsureNotExpired(RecipeImportStagingSession session)
    {
        if (session.ExpiresUtc <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("Import session has expired.");
        }
    }
}
