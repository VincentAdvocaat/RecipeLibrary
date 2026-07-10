namespace RecipeLibrary.Application.Pantry;

public static class PantryOwnerKey
{
    public static string Resolve(string? ownerUserId, Guid shoppingListGroupId) =>
        !string.IsNullOrWhiteSpace(ownerUserId)
            ? ownerUserId
            : $"group:{shoppingListGroupId:D}";

    public static void Validate(string ownerKey)
    {
        if (string.IsNullOrWhiteSpace(ownerKey))
        {
            throw new ArgumentException("Pantry owner key is required.");
        }

        if (ownerKey.Length > 256)
        {
            throw new ArgumentException("Pantry owner key must be at most 256 characters.");
        }
    }
}
