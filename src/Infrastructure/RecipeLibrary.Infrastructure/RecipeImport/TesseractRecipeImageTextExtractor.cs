using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;
using Tesseract;

namespace RecipeLibrary.Infrastructure.RecipeImport;

public sealed class TesseractRecipeImageTextExtractor(IOptions<RecipeImportOptions> options)
    : IRecipeImageTextExtractor
{
    public async Task<string> ExtractTextAsync(Stream imageStream, string language, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(imageStream);
        ct.ThrowIfCancellationRequested();

        using var memory = new MemoryStream();
        await imageStream.CopyToAsync(memory, ct);
        var bytes = memory.ToArray();
        var tessDataPath = ResolveTessDataPath(options.Value.Ocr.TessDataPath);

        return await Task.Run(() => ExtractSync(bytes, language, tessDataPath), ct);
    }

    private static string ExtractSync(byte[] bytes, string language, string tessDataPath)
    {
        try
        {
            if (!Directory.Exists(tessDataPath))
            {
                throw new InvalidOperationException("OCR is not available.");
            }

            using var engine = new TesseractEngine(tessDataPath, language, EngineMode.Default);
            using var pix = Pix.LoadFromMemory(bytes);
            using var page = engine.Process(pix);
            return page.GetText()?.Trim() ?? string.Empty;
        }
        catch (DllNotFoundException)
        {
            throw new InvalidOperationException("OCR is not available.");
        }
        catch (TesseractException)
        {
            throw new InvalidOperationException("OCR is not available.");
        }
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
