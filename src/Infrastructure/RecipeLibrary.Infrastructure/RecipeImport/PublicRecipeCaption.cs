using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.RecipeImport;

namespace RecipeLibrary.Infrastructure.RecipeImport;

internal static partial class PublicRecipeCaption
{
    public static string? Instagram(string html)
    {
        var document = new HtmlParser().ParseDocument(html);
        if (document.QuerySelector(".Caption") is { } embed)
        {
            foreach (var chrome in embed.QuerySelectorAll(".CaptionUsername, .CaptionComments"))
            {
                chrome.Remove();
            }

            var caption = new StringBuilder();
            foreach (var child in embed.ChildNodes)
            {
                caption.Append(child.NodeName.Equals("BR", StringComparison.OrdinalIgnoreCase)
                    ? '\n'
                    : child.TextContent);
            }

            if (caption.ToString().Trim() is { Length: > 0 } text)
            {
                return text;
            }
        }

        return document.QuerySelectorAll("meta[name='description'], meta[property='og:description'], meta[property='og:title']")
            .Select(meta => InstagramCaptionPattern().Match(meta.GetAttribute("content") ?? string.Empty))
            .Where(match => match.Success)
            .Select(match => match.Groups["caption"].Value.Trim())
            .OrderByDescending(caption => caption.Length)
            .FirstOrDefault(caption => caption.Length > 0);
    }

    public static string? YouTube(string html, string videoId)
    {
        var document = new HtmlParser().ParseDocument(html);
        foreach (var script in document.Scripts)
        {
            var scriptText = script.TextContent;
            var match = YouTubePlayerPattern().Match(scriptText);
            if (!match.Success)
            {
                continue;
            }

            var json = scriptText.AsSpan(match.Index + match.Length).TrimStart();
            if (json.IsEmpty || json[0] != '{')
            {
                continue;
            }

            try
            {
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json.ToString()));
                using var response = JsonDocument.ParseValue(ref reader);
                if (response.RootElement.TryGetProperty("videoDetails", out var details)
                    && details.ValueKind == JsonValueKind.Object
                    && details.TryGetProperty("videoId", out var id)
                    && id.ValueKind == JsonValueKind.String && id.GetString() == videoId)
                {
                    return RecipeSocialCaptionFetcher.ReadYouTubeText(details, "shortDescription");
                }
            }
            catch (JsonException)
            {
                // An incomplete or malformed script does not establish the caption.
            }
        }

        return null;
    }

    public static bool HasIngredientsAndSteps(string? caption)
    {
        if (string.IsNullOrWhiteSpace(caption))
        {
            return false;
        }

        var recipe = RecipeTextDocumentExtractor.Extract(caption);
        return recipe.IngredientLines.Count > 0 && recipe.Steps.Count > 0
            && !recipe.Warnings.Contains(ImportWarningCodes.HeuristicIngredients);
    }

    [GeneratedRegex("""\A[^"\r\n]*(?: on Instagram|\bcomments?\b[^"\r\n]*)\s*:\s*["“](?<caption>.+)["”]\.?\s*\z""",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.NonBacktracking)]
    private static partial Regex InstagramCaptionPattern();

    [GeneratedRegex(@"\bytInitialPlayerResponse\s*=\s*", RegexOptions.CultureInvariant)]
    private static partial Regex YouTubePlayerPattern();
}
