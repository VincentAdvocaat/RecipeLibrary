using Microsoft.AspNetCore.Identity;

namespace RecipeLibrary.Infrastructure.Identity;

/// <summary>
/// Application user stored by ASP.NET Core Identity (email + username).
/// </summary>
public sealed class ApplicationUser : IdentityUser
{
}
