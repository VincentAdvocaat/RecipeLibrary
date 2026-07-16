using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.Ingredients;

/// <summary>
/// Application-facing aliases; delegates to domain <see cref="UnitAliases"/>.
/// </summary>
public sealed record UnitAliasMatch(Unit Unit, decimal Multiplier);

public static class UnitAliasMap
{
    public static bool TryResolve(string? token, out UnitAliasMatch match)
    {
        if (UnitAliases.TryResolveMatch(token, out var domainMatch))
        {
            match = new UnitAliasMatch(domainMatch.Unit, domainMatch.Multiplier);
            return true;
        }

        match = default!;
        return false;
    }

    public static bool IsVagueQuantityWord(string? token) => UnitAliases.IsVagueQuantityWord(token);

    public static bool IsHandfulWord(string? token) => UnitAliases.IsHandfulWord(token);
}
