using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace RecipeLibrary.Application.Abstractions;

/// <summary>
/// Detects Blogger Dynamic Views shells (JS-rendered posts) and recovers post HTML via Atom.
/// </summary>
public static partial class BloggerDynamicContent
{
    private static readonly XNamespace AtomNs = "http://www.w3.org/2005/Atom";

    public static bool IsDynamicShell(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return false;
        }

        return DynamicTemplateMetaRegex().IsMatch(html);
    }

    public static bool TryGetPostId(string html, out string postId)
    {
        postId = string.Empty;
        if (string.IsNullOrWhiteSpace(html))
        {
            return false;
        }

        var match = PostIdRegex().Match(html);
        if (!match.Success)
        {
            return false;
        }

        postId = match.Groups[1].Value;
        return postId.Length > 0;
    }

    public static Uri BuildAtomFeedUri(Uri pageUri, string postId)
    {
        ArgumentNullException.ThrowIfNull(pageUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(postId);

        if (!Regex.IsMatch(postId, @"^\d+$"))
        {
            throw new ArgumentException("Blogger postId must be numeric.", nameof(postId));
        }

        var builder = new UriBuilder(pageUri.Scheme, pageUri.Host, pageUri.IsDefaultPort ? -1 : pageUri.Port)
        {
            Path = $"/feeds/posts/default/{postId}",
            Query = string.Empty,
            Fragment = string.Empty,
        };

        return builder.Uri;
    }

    /// <summary>
    /// When <paramref name="shellHtml"/> is a Blogger Dynamic Views shell, fetches Atom and wraps entry HTML.
    /// </summary>
    public static async Task<RecipeImportFetchedContent?> TryRecoverAsync(
        string shellHtml,
        Uri pageUri,
        Func<Uri, CancellationToken, Task<(string Body, bool WasTruncated)>> fetchAtomAsync,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pageUri);
        ArgumentNullException.ThrowIfNull(fetchAtomAsync);

        if (!IsDynamicShell(shellHtml) || !TryGetPostId(shellHtml, out var postId))
        {
            return null;
        }

        var feedUri = BuildAtomFeedUri(pageUri, postId);
        var (atom, wasTruncated) = await fetchAtomAsync(feedUri, ct);
        var entryHtml = TryExtractHtmlFromAtom(atom);
        if (string.IsNullOrWhiteSpace(entryHtml))
        {
            return null;
        }

        var title = TryGetEntryTitleFromAtom(atom);
        return new RecipeImportFetchedContent(WrapAsHtmlDocument(entryHtml, title), wasTruncated);
    }

    /// <summary>
    /// Extracts the HTML body of the Atom entry content element.
    /// </summary>
    public static string? TryExtractHtmlFromAtom(string atomXml)
    {
        if (string.IsNullOrWhiteSpace(atomXml))
        {
            return null;
        }

        try
        {
            var doc = XDocument.Parse(atomXml, LoadOptions.PreserveWhitespace);
            var entry = doc.Root?.Name == AtomNs + "entry"
                ? doc.Root
                : doc.Root?.Element(AtomNs + "entry");

            var content = entry?.Element(AtomNs + "content");
            if (content is null)
            {
                return null;
            }

            var type = (string?)content.Attribute("type") ?? "html";
            if (type.Contains("xhtml", StringComparison.OrdinalIgnoreCase))
            {
                return string.Concat(content.Nodes());
            }

            var value = content.Value;
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    public static string? TryGetEntryTitleFromAtom(string atomXml)
    {
        if (string.IsNullOrWhiteSpace(atomXml))
        {
            return null;
        }

        try
        {
            var doc = XDocument.Parse(atomXml, LoadOptions.None);
            var entry = doc.Root?.Name == AtomNs + "entry"
                ? doc.Root
                : doc.Root?.Element(AtomNs + "entry");
            var title = entry?.Element(AtomNs + "title")?.Value;
            return string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    public static string WrapAsHtmlDocument(string bodyHtml, string? title = null)
    {
        var safeTitle = string.IsNullOrWhiteSpace(title) ? "Recipe" : title.Trim();
        return $"""
            <!DOCTYPE html>
            <html><head><title>{WebUtility.HtmlEncode(safeTitle)}</title></head>
            <body>{bodyHtml}</body></html>
            """;
    }

    // Attribute order varies: content then name, or name then content.
    [GeneratedRegex(
        @"<meta\b[^>]*(?:\bname\s*=\s*['""]blogger-template['""][^>]*\bcontent\s*=\s*['""]dynamic['""]|\bcontent\s*=\s*['""]dynamic['""][^>]*\bname\s*=\s*['""]blogger-template['""])[^>]*/?>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DynamicTemplateMetaRegex();

    [GeneratedRegex(@"['""]postId['""]\s*:\s*['""](\d+)['""]", RegexOptions.CultureInvariant)]
    private static partial Regex PostIdRegex();
}
