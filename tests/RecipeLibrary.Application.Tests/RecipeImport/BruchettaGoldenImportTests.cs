using System.Text.Json;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.RecipeImport;
using RecipeLibrary.Application.Validators;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests.RecipeImport;

public sealed class BruchettaGoldenImportTests
{
    private readonly RecipeTextParser _parser = new(
        new IngredientLineParser(new IngredientLineResolver(new IngredientNameParser())));

    [Fact]
    public void Parse_CleanData_MatchesExpectedOutput_AndPassesValidator()
    {
        var clean = File.ReadAllText(GetFixturePath("clean-data.txt"));
        var expectedJson = File.ReadAllText(GetFixturePath("expected-output.json"));
        var expected = JsonSerializer.Deserialize<CreateRecipeCommand>(
            expectedJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Failed to deserialize expected-output.json");

        var parsed = _parser.Parse(clean);
        var actual = ImportRecipeResultMapper.ToCreateRecipeCommand(parsed);

        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.Description, actual.Description);
        Assert.Equal(expected.PreparationTimeMinutes, actual.PreparationTimeMinutes);
        Assert.Equal(expected.CookingTimeMinutes, actual.CookingTimeMinutes);
        Assert.Equal(expected.Difficulty, actual.Difficulty);
        Assert.Equal(expected.Category, actual.Category);
        Assert.Equal(expected.Servings, actual.Servings);
        Assert.Equal(expected.Ingredients.Count, actual.Ingredients.Count);

        for (var i = 0; i < expected.Ingredients.Count; i++)
        {
            Assert.Equal(expected.Ingredients[i].Name, actual.Ingredients[i].Name);
            Assert.Equal(expected.Ingredients[i].Preparation, actual.Ingredients[i].Preparation);
            Assert.Equal(expected.Ingredients[i].Quantity, actual.Ingredients[i].Quantity);
            Assert.Equal(expected.Ingredients[i].Unit, actual.Ingredients[i].Unit);
        }

        Assert.Equal(expected.InstructionSteps.Count, actual.InstructionSteps.Count);
        for (var i = 0; i < expected.InstructionSteps.Count; i++)
        {
            Assert.Equal(expected.InstructionSteps[i].StepNumber, actual.InstructionSteps[i].StepNumber);
            Assert.Equal(expected.InstructionSteps[i].Text, actual.InstructionSteps[i].Text);
        }

        CreateRecipeCommandValidator.ValidateAndThrow(actual);
        Assert.Equal((int)Difficulty.Easy, actual.Difficulty);
    }

    private static string GetFixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Bruchetta", fileName);
}
