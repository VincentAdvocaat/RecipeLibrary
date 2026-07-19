namespace RecipeLibrary.Application.Ingredients;

public interface IIngredientSimilarityScorer
{
    decimal Score(string normalizedInput, string normalizedCandidate);
}
