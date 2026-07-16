using System.Net.Http.Json;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Web.Models;

public static class RecipeIngredientSaveHelper
{
    public static async Task RefreshMatchStatesAsync(HttpClient http, IEnumerable<RecipeIngredientEditorItem> ingredients)
    {
        foreach (var ingredient in ingredients)
        {
            var input = ingredient.Name.Trim();
            if (input.Length == 0)
            {
                ingredient.RequiresConfirmation = false;
                ingredient.MatchSuggestions = [];
                continue;
            }

            if (ingredient.SaveResolution != IngredientSaveResolution.Pending)
            {
                continue;
            }

            var match = await http.PostAsJsonAsync("/ingredients/match", new MatchIngredientCommand { Input = input });
            var payload = await match.Content.ReadFromJsonAsync<MatchIngredientResult>();
            if (payload is null)
            {
                ingredient.RequiresConfirmation = false;
                ingredient.MatchSuggestions = [];
                continue;
            }

            ingredient.RequiresConfirmation = payload.RequiresConfirmation;
            ingredient.MatchSuggestions = payload.Suggestions.ToList();
            ingredient.LastMatchType = payload.MatchType;
        }
    }

    public static bool HasPendingConfirmations(IEnumerable<RecipeIngredientEditorItem> ingredients) =>
        ingredients.Any(x =>
            !string.IsNullOrWhiteSpace(x.Name)
            && x.RequiresConfirmation
            && x.SaveResolution == IngredientSaveResolution.Pending);

    public static IReadOnlyList<RecipeIngredientEditorItem> GetPendingConfirmations(IEnumerable<RecipeIngredientEditorItem> ingredients) =>
        ingredients
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.Name)
                && x.RequiresConfirmation
                && x.SaveResolution == IngredientSaveResolution.Pending)
            .ToList();

    public static List<CreateRecipeIngredientDto> ToIngredientDtos(IEnumerable<RecipeIngredientEditorItem> ingredients) =>
        ingredients.Select(x =>
        {
            var unmeasured = x.IsUnmeasured;
            return new CreateRecipeIngredientDto
            {
                Name = x.Name,
                Preparation = string.IsNullOrWhiteSpace(x.Preparation) ? null : x.Preparation.Trim(),
                Quantity = unmeasured ? null : x.Quantity,
                Unit = unmeasured ? null : x.UnitName,
                CreateAsNewIngredient = x.SaveResolution == IngredientSaveResolution.ConfirmedNew,
            };
        }).ToList();

    public static void ResetMatchState(RecipeIngredientEditorItem ingredient)
    {
        ingredient.SaveResolution = IngredientSaveResolution.Pending;
        ingredient.RequiresConfirmation = false;
        ingredient.MatchSuggestions = [];
        ingredient.LastMatchType = null;
    }
}
