using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RecipeLibrary.Infrastructure.Identity;

// Identity's normal managers use CancellationToken.None outside HTTP requests.
// These managers are resolved only inside a seed scope, with that operation's deadline.
public sealed class SeedUserManager(
    IUserStore<ApplicationUser> store,
    IOptions<IdentityOptions> options,
    IPasswordHasher<ApplicationUser> passwordHasher,
    IEnumerable<IUserValidator<ApplicationUser>> userValidators,
    IEnumerable<IPasswordValidator<ApplicationUser>> passwordValidators,
    ILookupNormalizer normalizer,
    IdentityErrorDescriber errors,
    IServiceProvider services,
    ILogger<UserManager<ApplicationUser>> logger)
    : UserManager<ApplicationUser>(store, options, passwordHasher, userValidators,
        passwordValidators, normalizer, errors, services, logger)
{
    public CancellationToken SeedToken { get; set; }

    protected override CancellationToken CancellationToken
    {
        get
        {
            SeedToken.ThrowIfCancellationRequested();
            return SeedToken;
        }
    }
}

public sealed class SeedRoleManager(
    IRoleStore<IdentityRole> store,
    IEnumerable<IRoleValidator<IdentityRole>> validators,
    ILookupNormalizer normalizer,
    IdentityErrorDescriber errors,
    ILogger<RoleManager<IdentityRole>> logger)
    : RoleManager<IdentityRole>(store, validators, normalizer, errors, logger)
{
    public CancellationToken SeedToken { get; set; }

    protected override CancellationToken CancellationToken
    {
        get
        {
            SeedToken.ThrowIfCancellationRequested();
            return SeedToken;
        }
    }
}
