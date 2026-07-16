using System.Globalization;
using System.Text.RegularExpressions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.Ingredients;

public sealed class IngredientLineParser(IngredientLineResolver lineResolver)
{
    private static readonly Regex QuantityPattern = new(
        @"^(?<value>\d+(?:[.,]\d+)?|\d+\s*/\s*\d+|\d+\s*\+\s*\d+\s*/\s*\d+|\d+\s*-\s*\d+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MixedPlusPattern = new(
        @"(\d+)\s*\+\s*(\d+)\s*/\s*(\d+)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public ParsedIngredientLine Parse(string? rawLine)
    {
        var raw = (rawLine ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return Unmeasured(raw, string.Empty, null, 0m);
        }

        var normalized = NormalizeLine(raw);
        var tokens = Tokenize(normalized);
        if (tokens.Count == 0)
        {
            return FallbackUnmeasured(raw, normalized, 0.35m);
        }

        if (UnitAliasMap.IsHandfulWord(tokens[0]))
        {
            return ParseHandful(raw, tokens);
        }

        if (UnitAliasMap.IsVagueQuantityWord(tokens[0]))
        {
            return ParseVagueQuantity(raw, tokens);
        }

        if (normalized.Contains("naar smaak", StringComparison.OrdinalIgnoreCase))
        {
            return ParseToTaste(raw, normalized);
        }

        var index = 0;
        if (!TryParseQuantityToken(tokens[index], out var quantity, out var rangeNote))
        {
            return FallbackUnmeasured(raw, normalized, 0.35m);
        }

        index++;

        // "1 8 sneetjes stokbrood" — list index before real quantity + count unit.
        if (index < tokens.Count
            && IsWholeNumberQuantity(quantity)
            && quantity is >= 1 and <= 20
            && TryParseQuantityToken(tokens[index], out var secondQuantity, out _)
            && IsWholeNumberQuantity(secondQuantity)
            && index + 1 < tokens.Count
            && UnitAliasMap.TryResolve(tokens[index + 1], out var peekUnit)
            && UnitRules.IsCountUnit(peekUnit.Unit)
            && peekUnit.Unit != Unit.Piece)
        {
            quantity = secondQuantity;
            rangeNote = null;
            index++;
        }

        if (index < tokens.Count
            && TryParseProperFractionToken(tokens[index], out var extraFraction))
        {
            quantity += extraFraction;
            index++;
        }

        Unit unit = Unit.Piece;
        decimal unitMultiplier = 1m;
        var hasExplicitUnit = false;

        if (index < tokens.Count && UnitAliasMap.TryResolve(tokens[index], out var unitMatch))
        {
            unit = unitMatch.Unit;
            unitMultiplier = unitMatch.Multiplier;
            hasExplicitUnit = true;
            index++;
        }

        var remainder = string.Join(' ', tokens.Skip(index)).Trim();
        var (name, explicitPrep) = IngredientPreparationSplitter.Split(remainder);
        var resolved = lineResolver.Resolve(name, explicitPrep);
        var scaled = quantity * unitMultiplier;
        decimal finalQuantity;
        if (UnitRules.AllowsCulinaryFractions(unit))
        {
            finalQuantity = CulinaryQuantityFractions.TrySnap(scaled, out var snapped)
                ? snapped
                : scaled;
        }
        else
        {
            finalQuantity = IngredientQuantityFormatter.Normalize(scaled, unit);
        }

        var confidence = hasExplicitUnit && resolved.DisplayName.Length > 0
            ? 0.95m
            : resolved.DisplayName.Length > 0
                ? 0.75m
                : 0.35m;

        var preparation = MergePreparation(rangeNote, resolved.Preparation);

        return new ParsedIngredientLine(
            raw,
            finalQuantity,
            unit.ToString(),
            resolved.DisplayName,
            preparation,
            confidence,
            ImportParseMethod.Deterministic);
    }

    private static ParsedIngredientLine ParseHandful(string raw, IReadOnlyList<string> tokens)
    {
        var remainder = string.Join(' ', tokens.Skip(1)).Trim();
        var (name, explicitPrep) = IngredientPreparationSplitter.Split(remainder);

        return new ParsedIngredientLine(
            raw,
            1,
            nameof(Unit.Handful),
            name,
            explicitPrep,
            0.85m,
            ImportParseMethod.Deterministic);
    }

    private static ParsedIngredientLine ParseVagueQuantity(string raw, IReadOnlyList<string> tokens)
    {
        var vagueWord = tokens[0];
        var remainder = string.Join(' ', tokens.Skip(1)).Trim();
        var (name, explicitPrep) = IngredientPreparationSplitter.Split(remainder);

        if (vagueWord.Equals("naar smaak", StringComparison.OrdinalIgnoreCase))
        {
            return Unmeasured(raw, name, "naar smaak", 0.55m);
        }

        var preparation = explicitPrep;
        return new ParsedIngredientLine(
            raw,
            1,
            nameof(Unit.Teaspoon),
            name,
            preparation,
            0.45m,
            ImportParseMethod.Deterministic);
    }

    private static ParsedIngredientLine ParseToTaste(string raw, string normalized)
    {
        var name = normalized
            .Replace("naar smaak", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim()
            .TrimEnd(',');

        var (cleanName, _) = IngredientPreparationSplitter.Split(name);
        return Unmeasured(raw, cleanName, "naar smaak", 0.55m);
    }

    private ParsedIngredientLine FallbackUnmeasured(string raw, string normalized, decimal confidence)
    {
        var (name, explicitPrep) = IngredientPreparationSplitter.Split(normalized);
        var resolved = lineResolver.Resolve(name, explicitPrep);
        var preparation = resolved.Preparation ?? "naar smaak";
        return Unmeasured(raw, resolved.DisplayName, preparation, confidence);
    }

    private static ParsedIngredientLine Unmeasured(string raw, string name, string? preparation, decimal confidence) =>
        new(
            raw,
            Quantity: null,
            Unit: null,
            Name: name,
            Preparation: preparation,
            Confidence: confidence,
            ParseMethod: ImportParseMethod.Deterministic);

    private static bool IsWholeNumberQuantity(decimal quantity) =>
        quantity == decimal.Truncate(quantity);

    private static string NormalizeLine(string raw)
    {
        var value = raw.Trim();
        value = value.TrimStart('-', '•', '*', '▢', ' ');
        value = ReplaceUnicodeFractions(value);
        value = NormalizeMixedPlus(value);
        return value.Trim();
    }

    private static string ReplaceUnicodeFractions(string value)
    {
        value = ReplaceMixedUnicode(value, '¼', CulinaryQuantityFractions.Quarter);
        value = ReplaceMixedUnicode(value, '½', CulinaryQuantityFractions.Half);
        value = ReplaceMixedUnicode(value, '¾', CulinaryQuantityFractions.ThreeQuarters);
        value = ReplaceMixedUnicode(value, '⅓', CulinaryQuantityFractions.Third);
        value = ReplaceMixedUnicode(value, '⅔', CulinaryQuantityFractions.TwoThirds);

        value = value.Replace("¼", " 1/4 ", StringComparison.Ordinal);
        value = value.Replace("½", " 1/2 ", StringComparison.Ordinal);
        value = value.Replace("¾", " 3/4 ", StringComparison.Ordinal);
        value = value.Replace("⅓", " 1/3 ", StringComparison.Ordinal);
        value = value.Replace("⅔", " 2/3 ", StringComparison.Ordinal);

        return Regex.Replace(value, @"\s+", " ").Trim();
    }

    private static string ReplaceMixedUnicode(string value, char vulgar, decimal fraction)
    {
        return Regex.Replace(
            value,
            $@"(\d+)\s*{Regex.Escape(vulgar.ToString())}",
            match =>
            {
                var whole = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                return " " + (whole + fraction).ToString(CultureInfo.InvariantCulture) + " ";
            },
            RegexOptions.CultureInvariant);
    }

    private static string NormalizeMixedPlus(string value) =>
        MixedPlusPattern.Replace(
            value,
            match =>
            {
                var whole = decimal.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                var numerator = decimal.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                var denominator = decimal.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                if (denominator == 0)
                {
                    return match.Value;
                }

                return " " + (whole + (numerator / denominator)).ToString(CultureInfo.InvariantCulture) + " ";
            });

    private static List<string> Tokenize(string normalized)
    {
        if (normalized.Length == 0)
        {
            return [];
        }

        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.ToList();
    }

    private static bool TryParseQuantityToken(string token, out decimal quantity, out string? rangeNote)
    {
        rangeNote = null;
        quantity = 0;

        var match = QuantityPattern.Match(token);
        if (!match.Success)
        {
            return false;
        }

        var value = match.Groups["value"].Value.Replace(" ", string.Empty, StringComparison.Ordinal);

        if (value.Contains('+', StringComparison.Ordinal))
        {
            var plusParts = value.Split('+', 2);
            if (plusParts.Length == 2
                && decimal.TryParse(plusParts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var whole)
                && TryParseFractionLiteral(plusParts[1], out var fractionPart))
            {
                quantity = whole + fractionPart;
                return true;
            }

            return false;
        }

        if (value.Contains('/', StringComparison.Ordinal))
        {
            return TryParseFractionLiteral(value, out quantity);
        }

        if (value.Contains('-', StringComparison.Ordinal))
        {
            var rangeParts = value.Split('-');
            if (rangeParts.Length == 2
                && decimal.TryParse(rangeParts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var lower)
                && decimal.TryParse(rangeParts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            {
                quantity = lower;
                rangeNote = value;
                return true;
            }

            return false;
        }

        var normalized = value.Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out quantity);
    }

    private static bool TryParseProperFractionToken(string token, out decimal fraction)
    {
        fraction = 0m;
        if (!TryParseFractionLiteral(token, out var value))
        {
            return false;
        }

        if (value <= 0m || value >= 1m)
        {
            return false;
        }

        fraction = value;
        return true;
    }

    private static bool TryParseFractionLiteral(string value, out decimal quantity)
    {
        quantity = 0m;
        var fractionParts = value.Split('/');
        if (fractionParts.Length == 2
            && decimal.TryParse(fractionParts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var numerator)
            && decimal.TryParse(fractionParts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var denominator)
            && denominator != 0)
        {
            quantity = numerator / denominator;
            return true;
        }

        return false;
    }

    private static string? MergePreparation(string? rangeNote, string? preparation)
    {
        if (rangeNote is null)
        {
            return preparation;
        }

        if (preparation is null)
        {
            return rangeNote;
        }

        return $"{rangeNote}; {preparation}";
    }
}

public sealed record ParsedIngredientLine(
    string RawLine,
    decimal? Quantity,
    string? Unit,
    string Name,
    string? Preparation,
    decimal Confidence,
    ImportParseMethod ParseMethod);
