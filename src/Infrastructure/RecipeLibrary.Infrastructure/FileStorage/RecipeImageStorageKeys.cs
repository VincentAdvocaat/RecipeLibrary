namespace RecipeLibrary.Infrastructure.FileStorage;

internal static class RecipeImageStorageKeys
{
    /// <summary>
    /// Builds "{ownerUserId}_{guid:N}{ext}" so pending uploads can be authorized without a DB row.
    /// </summary>
    public static string Create(string ownerUserId, string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        var owner = ownerUserId.Trim();
        if (owner.IndexOfAny(['/', '\\', '_']) >= 0)
        {
            throw new ArgumentException("Owner user id contains invalid characters for image storage keys.", nameof(ownerUserId));
        }

        return $"{owner}_{Guid.NewGuid():N}{extension}";
    }
}
