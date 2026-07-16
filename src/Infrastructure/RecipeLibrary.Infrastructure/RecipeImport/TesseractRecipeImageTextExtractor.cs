using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;
using Tesseract;

namespace RecipeLibrary.Infrastructure.RecipeImport;

public sealed class TesseractRecipeImageTextExtractor(IOptions<RecipeImportOptions> options)
    : IRecipeImageTextExtractor
{
    public Task<string> ExtractTextAsync(Stream imageStream, string language, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(imageStream);
        ct.ThrowIfCancellationRequested();

        var tessDataPath = ResolveTessDataPath(options.Value.Ocr.TessDataPath);
        if (!Directory.Exists(tessDataPath))
        {
            throw new InvalidOperationException($"OCR tessdata folder was not found at '{tessDataPath}'.");
        }

        using var engine = new TesseractEngine(tessDataPath, language, EngineMode.Default);
        using var memory = new MemoryStream();
        imageStream.CopyTo(memory);
        var bytes = memory.ToArray();
        using var pix = Pix.LoadFromMemory(bytes);
        using var page = engine.Process(pix);
        var text = page.GetText()?.Trim() ?? string.Empty;
        return Task.FromResult(text);
    }

    private static string ResolveTessDataPath(string configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        return Path.Combine(AppContext.BaseDirectory, "tessdata");
    }
}
