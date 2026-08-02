using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Validators;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class CreateRecipeCommandValidatorTests
{
    [Fact]
    public void Validate_Throws_WhenTitleEmpty()
    {
        var command = ValidCommand(title: "  ");

        var ex = Assert.Throws<ArgumentException>(() => CreateRecipeCommandValidator.ValidateAndThrow(command));
        Assert.Contains("Title", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_AcceptsIngredientWithPreparation()
    {
        var command = ValidCommand(preparation: "ruim");

        CreateRecipeCommandValidator.ValidateAndThrow(command);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_AcceptsNullOrEmptyImageUrl(string? imageUrl)
    {
        var command = ValidCommand(imageUrl: imageUrl);

        CreateRecipeCommandValidator.ValidateAndThrow(command);
    }

    [Fact]
    public void Validate_AcceptsAppRelativeRecipeImagePath()
    {
        var fileName = $"{Guid.NewGuid():D}.jpg";
        var command = ValidCommand(imageUrl: $"/api/recipe-images/{fileName}");

        CreateRecipeCommandValidator.ValidateAndThrow(command);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://evil.com/x.png")]
    [InlineData("//evil.com/x")]
    [InlineData("/api/other/x")]
    public void Validate_RejectsUnsafeOrNonRecipeImageUrls(string imageUrl)
    {
        var command = ValidCommand(imageUrl: imageUrl);

        var ex = Assert.Throws<ArgumentException>(() => CreateRecipeCommandValidator.ValidateAndThrow(command));
        Assert.Equal(nameof(CreateRecipeCommand.ImageUrl), ex.ParamName);
    }

    private static CreateRecipeCommand ValidCommand(
        string title = "Test Recipe",
        string? preparation = null,
        string? imageUrl = null) => new()
    {
        Title = title,
        Category = 2,
        PreparationTimeMinutes = 10,
        CookingTimeMinutes = 20,
        ImageUrl = imageUrl,
        Ingredients =
        [
            new CreateRecipeIngredientDto
            {
                Name = "Gehakt",
                Preparation = preparation,
                Quantity = 500,
                Unit = Unit.Gram.ToString(),
            },
        ],
        InstructionSteps =
        [
            new CreateRecipeInstructionStepDto { StepNumber = 1, Text = "Cook." },
        ],
    };
}
