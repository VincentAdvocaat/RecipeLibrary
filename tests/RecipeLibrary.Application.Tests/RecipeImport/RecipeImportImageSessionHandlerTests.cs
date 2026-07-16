using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.RecipeImport;
using RecipeLibrary.Application.UseCases.RecipeImport;
using Xunit;

namespace RecipeLibrary.Application.Tests.RecipeImport;

public sealed class RecipeImportImageSessionHandlerTests
{
    [Fact]
    public async Task ProcessSession_ConcatenatesOcrTextInOrder_AndDeletesSession()
    {
        var store = new InMemoryStagingStore();
        var session = await store.CreateSessionAsync("user-1", TimeSpan.FromMinutes(30));
        await store.AddImageAsync(session.SessionId, "user-1", new MemoryStream([1]), "a.png", "image/png", 5);
        await store.AddImageAsync(session.SessionId, "user-1", new MemoryStream([2]), "b.png", "image/png", 5);

        var extractor = new SequencingExtractor(["first page", "second page"]);
        var sut = new ProcessRecipeImportImageSessionQueryHandler(
            store,
            extractor,
            CreateService(),
            NullLogger<ProcessRecipeImportImageSessionQueryHandler>.Instance);

        var result = await sut.HandleAsync(new ProcessRecipeImportImageSessionQuery
        {
            SessionId = session.SessionId,
            OwnerKey = "user-1",
            Language = "eng",
        });

        Assert.NotNull(result);
        Assert.Equal(["eng", "eng"], extractor.Languages);
        Assert.Null(await store.GetSessionAsync(session.SessionId));
    }

    [Fact]
    public async Task ProcessSession_DeletesSession_WhenOcrFails()
    {
        var store = new InMemoryStagingStore();
        var session = await store.CreateSessionAsync("", TimeSpan.FromMinutes(30));
        await store.AddImageAsync(session.SessionId, "", new MemoryStream([1]), "a.png", "image/png", 5);

        var sut = new ProcessRecipeImportImageSessionQueryHandler(
            store,
            new SequencingExtractor([""]),
            CreateService(),
            NullLogger<ProcessRecipeImportImageSessionQueryHandler>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.HandleAsync(new ProcessRecipeImportImageSessionQuery
            {
                SessionId = session.SessionId,
                OwnerKey = "",
                Language = "nld",
            }));

        Assert.Null(await store.GetSessionAsync(session.SessionId));
    }

    [Fact]
    public async Task AddImage_Throws_WhenMaxImagesExceeded()
    {
        var options = Options.Create(new RecipeImportOptions
        {
            Ocr = new RecipeImportOcrOptions { MaxImagesPerSession = 1, MaxImageBytes = 1024 },
        });
        var store = new InMemoryStagingStore();
        var session = await store.CreateSessionAsync("", TimeSpan.FromMinutes(30));
        var sut = new AddRecipeImportImageCommandHandler(store, options);

        await sut.HandleAsync(new AddRecipeImportImageCommand
        {
            SessionId = session.SessionId,
            ImageBytes = [1, 2, 3],
            ContentType = "image/png",
            FileName = "one.png",
        });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.HandleAsync(new AddRecipeImportImageCommand
            {
                SessionId = session.SessionId,
                ImageBytes = [4, 5, 6],
                ContentType = "image/png",
                FileName = "two.png",
            }));
    }

    [Fact]
    public async Task ProcessSession_Throws_WhenExpired()
    {
        var store = new InMemoryStagingStore();
        var session = await store.CreateSessionAsync("", TimeSpan.FromMinutes(-1));
        await store.AddImageAsync(session.SessionId, "", new MemoryStream([1]), "a.png", "image/png", 5, skipExpiryCheck: true);

        var sut = new ProcessRecipeImportImageSessionQueryHandler(
            store,
            new SequencingExtractor(["x"]),
            CreateService(),
            NullLogger<ProcessRecipeImportImageSessionQueryHandler>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.HandleAsync(new ProcessRecipeImportImageSessionQuery
            {
                SessionId = session.SessionId,
                OwnerKey = "",
            }));
    }

    [Fact]
    public async Task DeleteExpiredSessions_RemovesOldSessions()
    {
        var store = new InMemoryStagingStore();
        var expired = await store.CreateSessionAsync("", TimeSpan.FromMinutes(-5));
        var active = await store.CreateSessionAsync("", TimeSpan.FromMinutes(30));

        var removed = await store.DeleteExpiredSessionsAsync();

        Assert.Equal(1, removed);
        Assert.Null(await store.GetSessionAsync(expired.SessionId));
        Assert.NotNull(await store.GetSessionAsync(active.SessionId));
    }

    private static RecipeImportService CreateService() =>
        new(
            new StructuredRecipeExtractor(),
            new IngredientLineParser(new IngredientLineResolver(new IngredientNameParser())),
            new IngredientMatcher(new EmptyIngredientRepository(), new IngredientTextNormalizer(), new IngredientSimilarityScorer()),
            new NullIngredientLineAiParser(),
            Options.Create(new RecipeImportOptions()));

    private sealed class NullIngredientLineAiParser : IIngredientLineAiParser
    {
        public Task<IReadOnlyList<AiParsedIngredientLine>> ParseLinesAsync(IReadOnlyList<string> rawLines, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AiParsedIngredientLine>>([]);
    }

    private sealed class EmptyIngredientRepository : IIngredientRepository
    {
        public Task AddMatchLogAsync(Domain.Entities.IngredientMatchLog log, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddTagsAsync(Guid ingredientId, IReadOnlyList<(string Name, string NormalizedName)> tags, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Domain.Entities.CanonicalIngredient> CreateIngredientWithAliasAsync(string canonicalName, string normalizedName, string alias, string normalizedAlias, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Domain.Entities.CanonicalIngredient>> GetFuzzyCandidatesAsync(string normalizedQuery, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Domain.Entities.CanonicalIngredient>>([]);
        public Task<Domain.Entities.CanonicalIngredient?> GetByNormalizedAliasAsync(string normalizedAlias, CancellationToken ct = default) => Task.FromResult<Domain.Entities.CanonicalIngredient?>(null);
        public Task<Domain.Entities.CanonicalIngredient?> GetByNormalizedNameAsync(string normalizedName, CancellationToken ct = default) => Task.FromResult<Domain.Entities.CanonicalIngredient?>(null);
        public Task<IReadOnlyList<Domain.Entities.CanonicalIngredient>> SearchAsync(string normalizedQuery, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Domain.Entities.CanonicalIngredient>>([]);
        public Task<IReadOnlyList<Domain.Entities.Tag>> SearchTagsAsync(string normalizedQuery, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Domain.Entities.Tag>>([]);
    }

    private sealed class SequencingExtractor(IReadOnlyList<string> texts) : IRecipeImageTextExtractor
    {
        private int _index;
        public List<string> Languages { get; } = [];

        public Task<string> ExtractTextAsync(Stream imageStream, string language, CancellationToken ct = default)
        {
            Languages.Add(language);
            var text = _index < texts.Count ? texts[_index] : string.Empty;
            _index++;
            return Task.FromResult(text);
        }
    }

    private sealed class InMemoryStagingStore : IRecipeImportStagingStore
    {
        private readonly Dictionary<string, RecipeImportStagingSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, byte[]> _blobs = new(StringComparer.OrdinalIgnoreCase);

        public Task<RecipeImportStagingSession> CreateSessionAsync(string ownerKey, TimeSpan ttl, CancellationToken ct = default)
        {
            var now = DateTimeOffset.UtcNow;
            var session = new RecipeImportStagingSession
            {
                SessionId = Guid.NewGuid().ToString("N"),
                OwnerKey = ownerKey ?? string.Empty,
                CreatedUtc = now,
                ExpiresUtc = now.Add(ttl),
                Images = [],
            };
            _sessions[session.SessionId] = Clone(session);
            return Task.FromResult(Clone(session));
        }

        public Task<RecipeImportStagingSession?> GetSessionAsync(string sessionId, CancellationToken ct = default) =>
            Task.FromResult(_sessions.TryGetValue(sessionId, out var session) ? Clone(session) : null);

        public Task<RecipeImportStagingImage> AddImageAsync(
            string sessionId,
            string ownerKey,
            Stream content,
            string fileName,
            string contentType,
            int maxImages,
            CancellationToken ct = default) =>
            AddImageAsync(sessionId, ownerKey, content, fileName, contentType, maxImages, skipExpiryCheck: false, ct);

        public async Task<RecipeImportStagingImage> AddImageAsync(
            string sessionId,
            string ownerKey,
            Stream content,
            string fileName,
            string contentType,
            int maxImages,
            bool skipExpiryCheck,
            CancellationToken ct = default)
        {
            var session = Require(sessionId, ownerKey, skipExpiryCheck);
            if (session.Images.Count >= maxImages)
            {
                throw new ArgumentException($"A maximum of {maxImages} images is allowed per import session.");
            }

            await using var ms = new MemoryStream();
            await content.CopyToAsync(ms, ct);
            var image = new RecipeImportStagingImage
            {
                ImageId = Guid.NewGuid().ToString("N"),
                Order = session.Images.Count + 1,
                FileName = fileName,
                ContentType = contentType,
                CreatedUtc = DateTimeOffset.UtcNow,
            };
            session.Images.Add(image);
            _blobs[$"{sessionId}/{image.ImageId}"] = ms.ToArray();
            _sessions[sessionId] = Clone(session);
            return image;
        }

        public Task RemoveImageAsync(string sessionId, string ownerKey, string imageId, CancellationToken ct = default)
        {
            var session = Require(sessionId, ownerKey, skipExpiryCheck: false);
            session.Images.RemoveAll(x => x.ImageId == imageId);
            _blobs.Remove($"{sessionId}/{imageId}");
            _sessions[sessionId] = Clone(session);
            return Task.CompletedTask;
        }

        public Task<(Stream Stream, string ContentType)?> OpenImageAsync(
            string sessionId,
            string ownerKey,
            string imageId,
            CancellationToken ct = default)
        {
            var session = Require(sessionId, ownerKey, skipExpiryCheck: false);
            var image = session.Images.FirstOrDefault(x => x.ImageId == imageId);
            if (image is null || !_blobs.TryGetValue($"{sessionId}/{imageId}", out var bytes))
            {
                return Task.FromResult<(Stream, string)?>(null);
            }

            return Task.FromResult<(Stream, string)?>((new MemoryStream(bytes), image.ContentType));
        }

        public Task DeleteSessionAsync(string sessionId, CancellationToken ct = default)
        {
            _sessions.Remove(sessionId);
            foreach (var key in _blobs.Keys.Where(k => k.StartsWith(sessionId + "/", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                _blobs.Remove(key);
            }

            return Task.CompletedTask;
        }

        public async Task<int> DeleteExpiredSessionsAsync(CancellationToken ct = default)
        {
            var expired = _sessions.Values.Where(s => s.ExpiresUtc <= DateTimeOffset.UtcNow).Select(s => s.SessionId).ToList();
            foreach (var id in expired)
            {
                await DeleteSessionAsync(id, ct);
            }

            return expired.Count;
        }

        private RecipeImportStagingSession Require(string sessionId, string ownerKey, bool skipExpiryCheck)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                throw new InvalidOperationException("Import session was not found or has expired.");
            }

            if (!string.IsNullOrEmpty(session.OwnerKey)
                && !string.Equals(session.OwnerKey, ownerKey ?? string.Empty, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException("Import session does not belong to the current user.");
            }

            if (!skipExpiryCheck && session.ExpiresUtc <= DateTimeOffset.UtcNow)
            {
                throw new InvalidOperationException("Import session has expired.");
            }

            return Clone(session);
        }

        private static RecipeImportStagingSession Clone(RecipeImportStagingSession session) =>
            new()
            {
                SessionId = session.SessionId,
                OwnerKey = session.OwnerKey,
                CreatedUtc = session.CreatedUtc,
                ExpiresUtc = session.ExpiresUtc,
                Images = session.Images.Select(x => new RecipeImportStagingImage
                {
                    ImageId = x.ImageId,
                    Order = x.Order,
                    FileName = x.FileName,
                    ContentType = x.ContentType,
                    CreatedUtc = x.CreatedUtc,
                }).ToList(),
            };
    }
}
