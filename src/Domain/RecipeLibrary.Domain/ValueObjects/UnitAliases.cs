namespace RecipeLibrary.Domain.ValueObjects;

/// <summary>
/// Token aliases for <see cref="Unit"/> (Dutch and shorthand), including multipliers (kg → gram).
/// </summary>
public static class UnitAliases
{
    public readonly record struct Match(Unit Unit, decimal Multiplier);

    private static readonly Dictionary<string, Match> Aliases = BuildAliases();

    public static bool TryResolve(string? token, out Unit unit)
    {
        if (TryResolveMatch(token, out var match))
        {
            unit = match.Unit;
            return true;
        }

        unit = Unit.Unknown;
        return false;
    }

    public static bool TryResolveMatch(string? token, out Match match)
    {
        var key = (token ?? string.Empty).Trim().TrimEnd('.', '/').ToLowerInvariant();
        if (key.Length == 0)
        {
            match = default;
            return false;
        }

        return Aliases.TryGetValue(key, out match!);
    }

    public static bool IsVagueQuantityWord(string? token)
    {
        var key = (token ?? string.Empty).Trim().ToLowerInvariant();
        return key is "snuf" or "snufje" or "snufjes" or "beetje" or "naar smaak";
    }

    public static bool IsHandfulWord(string? token)
    {
        var key = (token ?? string.Empty).Trim().ToLowerInvariant();
        return key is "handje" or "handjes";
    }

    private static Dictionary<string, Match> BuildAliases()
    {
        var map = new Dictionary<string, Match>(StringComparer.OrdinalIgnoreCase);

        void Add(string alias, Unit unit, decimal multiplier = 1m) =>
            map[alias] = new Match(unit, multiplier);

        Add("g", Unit.Gram);
        Add("gr", Unit.Gram);
        Add("gm", Unit.Gram);
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
        Add("teaspoon", Unit.Teaspoon);
        Add("teaspoons", Unit.Teaspoon);

        Add("el", Unit.Tablespoon);
        Add("eetlepel", Unit.Tablespoon);
        Add("eetlepels", Unit.Tablespoon);
        Add("tbsp", Unit.Tablespoon);
        Add("tbs", Unit.Tablespoon);
        Add("tablespoon", Unit.Tablespoon);
        Add("tablespoons", Unit.Tablespoon);

        Add("cup", Unit.Cup);
        Add("cups", Unit.Cup);
        Add("kop", Unit.Cup);
        Add("kopje", Unit.Cup);
        Add("kopjes", Unit.Cup);

        Add("oz", Unit.Ounce);
        Add("ounce", Unit.Ounce);
        Add("ounces", Unit.Ounce);

        Add("lb", Unit.Pound);
        Add("lbs", Unit.Pound);
        Add("pound", Unit.Pound);
        Add("pounds", Unit.Pound);

        Add("can", Unit.Can);
        Add("cans", Unit.Can);
        Add("blik", Unit.Can);
        Add("blikje", Unit.Can);
        Add("blikjes", Unit.Can);

        Add("st", Unit.Piece);
        Add("stuk", Unit.Piece);
        Add("stuks", Unit.Piece);
        Add("x", Unit.Piece);
        Add("piece", Unit.Piece);
        Add("pieces", Unit.Piece);

        Add("snuf", Unit.Teaspoon);
        Add("snufje", Unit.Teaspoon);
        Add("snufjes", Unit.Teaspoon);

        Add("teen", Unit.Clove);
        Add("teentje", Unit.Clove);
        Add("teentjes", Unit.Clove);
        Add("clove", Unit.Clove);
        Add("cloves", Unit.Clove);

        Add("handje", Unit.Handful);
        Add("handjes", Unit.Handful);
        Add("handful", Unit.Handful);

        Add("sneetje", Unit.Slice);
        Add("sneetjes", Unit.Slice);
        Add("snee", Unit.Slice);
        Add("plakje", Unit.Slice);
        Add("plakjes", Unit.Slice);
        Add("slice", Unit.Slice);

        Add("takje", Unit.Sprig);
        Add("takjes", Unit.Sprig);
        Add("sprig", Unit.Sprig);

        Add("blaadje", Unit.Leaf);
        Add("blaadjes", Unit.Leaf);
        Add("leaf", Unit.Leaf);

        Add("bosje", Unit.Bunch);
        Add("bosjes", Unit.Bunch);
        Add("bunch", Unit.Bunch);

        Add("stengel", Unit.Stalk);
        Add("stengels", Unit.Stalk);
        Add("stalk", Unit.Stalk);

        return map;
    }
}
