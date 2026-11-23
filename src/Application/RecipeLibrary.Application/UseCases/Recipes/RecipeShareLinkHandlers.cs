using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.UseCases.Recipes;

public sealed class RecipeShareLinkHandlers(
    IRecipeShareRepository shares,
    ICurrentUser currentUser,
    IDemoContext demo)
    : IQueryHandler<GetRecipeShareLinkQuery, Guid?>, ICommandHandler<SetRecipeShareLinkCommand, Guid?>
{
    public Task<Guid?> HandleAsync(GetRecipeShareLinkQuery query, CancellationToken ct = default) =>
        shares.GetLinkKeyAsync(RequireAccount(), query.RecipeId, ct);

    public Task<Guid?> HandleAsync(SetRecipeShareLinkCommand command, CancellationToken ct = default) =>
        shares.SetLinkAsync(RequireAccount(), command.RecipeId, command.Enabled, ct);

    private string RequireAccount() => !demo.IsDemo && currentUser.IsAuthenticated && currentUser.UserId is { } userId
        ? userId
        : throw new UnauthorizedAccessException("An account is required to share recipes.");
}
