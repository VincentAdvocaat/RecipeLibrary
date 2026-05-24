using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.Ingredients;

public sealed record UnitAliasMatch(Unit Unit, decimal Multiplier);

public static class UnitAliasMap
{
    private static readonly Dictionary<string, UnitAliasMatch> Aliases = BuildAliases();

    public static bool TryResolve(string? token, out UnitAliasMatch match)
    {
        var key = (token ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();
        if (key.Length == 0)
        {
            match = default!;
            return false;
        }

        return Aliases.TryGetValue(key, out match!);
    }

    public static bool IsVagueQuantityWord(string? token)
    {
        var key = (token ?? string.Empty).Trim().ToLowerInvariant();
        return key is "snuf" or "snufje" or "snufjes" or "handje" or "handjes" or "beetje" or "naar smaak";
    }

    private static Dictionary<string, UnitAliasMatch> BuildAliases()
    {
        var map = new Dictionary<string, UnitAliasMatch>(StringComparer.OrdinalIgnoreCase);

        void Add(string alias, Unit unit, decimal multiplier = 1m) =>
            map[alias] = new UnitAliasMatch(unit, multiplier);

        Add("g", Unit.Gram);
        Add("gr", Unit.Gram);
        Add("gram", Unit.Gram);
        Add("grammen", Unit.Gram);

        Add("kg", Unit.Gram, 1000m);
        Add("kilogram", Unit.Gram, 1000m);
        Add("kilo", Unit.Gram, 1000m);

        Add("ml", Unit.Milliliter);
        Add("milliliter", Unit.Milliliter);

        Add("cl", Unit.Milliliter, 10m);
        Add("dl", Unit.Milliliter, 100m);

        Add("tl", Unit.Teaspoon);
        Add("theelepel", Unit.Teaspoon);
        Add("theelepels", Unit.Teaspoon);
        Add("tsp", Unit.Teaspoon);

        Add("el", Unit.Tablespoon);
        Add("eetlepel", Unit.Tablespoon);
        Add("eetlepels", Unit.Tablespoon);
        Add("tbsp", Unit.Tablespoon);

        Add("st", Unit.Piece);
        Add("stuk", Unit.Piece);
        Add("stuks", Unit.Piece);
        Add("x", Unit.Piece);

        Add("snuf", Unit.Teaspoon);
        Add("snufje", Unit.Teaspoon);
        Add("snufjes", Unit.Teaspoon);

        return map;
    }
}
