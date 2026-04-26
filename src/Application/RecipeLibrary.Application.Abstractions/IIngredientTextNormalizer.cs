namespace RecipeLibrary.Application.Abstractions;

public interface IIngredientTextNormalizer
{
    string Normalize(string? input);
}
