namespace RecipeLibrary.Application.Ingredients;

public sealed class IngredientNameParser
{
    public ParsedIngredient ParseIngredient(string? input)
    {
        var (name, preparation) = IngredientPreparationSplitter.Split(input);
        return new ParsedIngredient(name, preparation);
    }
}

public sealed record ParsedIngredient(string Name, string? Preparation);
