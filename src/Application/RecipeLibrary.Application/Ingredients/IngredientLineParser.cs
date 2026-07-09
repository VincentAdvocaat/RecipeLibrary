using System.Globalization;
using System.Text.RegularExpressions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.Ingredients;

public sealed class IngredientLineParser(IngredientLineResolver lineResolver)
{
    private static readonly Regex QuantityPattern = new(
        @"^(?<value>\d+(?:[.,]\d+)?|\d+\s*/\s*\d+|\d+\s*-\s*\d+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public ParsedIngredientLine Parse(string? rawLine)
    {
        var raw = (rawLine ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return new ParsedIngredientLine(
                raw,
                1,
                nameof(Unit.Piece),
                string.Empty,
                null,
                0m,
                ImportParseMethod.Deterministic);
        }

        var normalized = NormalizeLine(raw);
        var tokens = Tokenize(normalized);
        if (tokens.Count == 0)
        {
            return Fallback(raw, normalized, 0.35m);
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
            return Fallback(raw, normalized, 0.35m);
        }

        index++;

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
        var (name, explicitPrep) = SplitNameAndPreparation(remainder);
        var resolved = lineResolver.Resolve(name, explicitPrep);
        var finalQuantity = IngredientQuantityFormatter.Normalize(quantity * unitMultiplier, unit);

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

    private static ParsedIngredientLine ParseVagueQuantity(string raw, IReadOnlyList<string> tokens)
    {
        var vagueWord = tokens[0];
        var remainder = string.Join(' ', tokens.Skip(1)).Trim();
        var (name, explicitPrep) = SplitNameAndPreparation(remainder);

        var unit = vagueWord.Equals("naar smaak", StringComparison.OrdinalIgnoreCase)
            ? Unit.Piece
            : Unit.Teaspoon;

        var preparation = vagueWord.Equals("naar smaak", StringComparison.OrdinalIgnoreCase)
            ? "naar smaak"
            : explicitPrep;

        if (!vagueWord.Equals("naar smaak", StringComparison.OrdinalIgnoreCase) && explicitPrep is not null)
        {
            preparation = explicitPrep;
        }

        return new ParsedIngredientLine(
            raw,
            1,
            unit.ToString(),
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

        return new ParsedIngredientLine(
            raw,
            1,
            nameof(Unit.Piece),
            name,
            "naar smaak",
            0.35m,
            ImportParseMethod.Deterministic);
    }

    private ParsedIngredientLine Fallback(string raw, string normalized, decimal confidence)
    {
        var (name, explicitPrep) = SplitNameAndPreparation(normalized);
        var resolved = lineResolver.Resolve(name, explicitPrep);

        return new ParsedIngredientLine(
            raw,
            1,
            nameof(Unit.Piece),
            resolved.DisplayName,
            resolved.Preparation,
            confidence,
            ImportParseMethod.Deterministic);
    }

    private static string NormalizeLine(string raw)
    {
        var value = raw.Trim();
        value = value.TrimStart('-', '•', '*', '▢', ' ');
        value = ReplaceUnicodeFractions(value);
        return value.Trim();
    }

    private static string ReplaceUnicodeFractions(string value)
    {
        value = value.Replace("½", " 0.5 ", StringComparison.Ordinal);
        value = value.Replace("¼", " 0.25 ", StringComparison.Ordinal);
        value = value.Replace("¾", " 0.75 ", StringComparison.Ordinal);
        value = value.Replace("⅓", " 0.333 ", StringComparison.Ordinal);
        value = value.Replace("⅔", " 0.667 ", StringComparison.Ordinal);

        var mixedMatch = Regex.Match(value, @"(\d+)\s*½");
        if (mixedMatch.Success)
        {
            var whole = decimal.Parse(mixedMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            value = value.Replace(mixedMatch.Value, (whole + 0.5m).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        return Regex.Replace(value, @"\s+", " ").Trim();
    }

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

        if (value.Contains('/', StringComparison.Ordinal))
        {
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

    private static (string Name, string? Preparation) SplitNameAndPreparation(string remainder)
    {
        if (remainder.Length == 0)
        {
            return (string.Empty, null);
        }

        var commaIndex = remainder.IndexOf(',');
        if (commaIndex < 0)
        {
            return (remainder.Trim(), null);
        }

        var name = remainder[..commaIndex].Trim();
        var preparation = remainder[(commaIndex + 1)..].Trim();
        return (name, preparation.Length > 0 ? preparation : null);
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
    decimal Quantity,
    string Unit,
    string Name,
    string? Preparation,
    decimal Confidence,
    ImportParseMethod ParseMethod);
