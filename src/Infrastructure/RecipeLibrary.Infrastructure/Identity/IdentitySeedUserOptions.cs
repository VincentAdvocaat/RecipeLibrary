namespace RecipeLibrary.Infrastructure.Identity;

/// <summary>
/// Development-only seed user settings (user-secrets / environment). Never commit real passwords.
/// </summary>
public sealed class IdentitySeedUserOptions
{
    public const string SectionName = "Identity:SeedUser";

    public string? Email { get; set; }

    public string? UserName { get; set; }

    public string? Password { get; set; }
}
