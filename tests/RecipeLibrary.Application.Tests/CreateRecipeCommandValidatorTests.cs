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

    [Theory]
    [InlineData(null, null)]
    [InlineData("  ", null)]
    [InlineData(" http://example.com/Recipe?q=1#steps ", "http://example.com/Recipe?q=1#steps")]
    [InlineData("HTTPS://EXAMPLE.COM/Recipe", "https://example.com/Recipe")]
    public void Validate_AcceptsOptionalWebSourceUrl(string? input, string? expected)
    {
        CreateRecipeCommandValidator.ValidateAndThrow(ValidCommand(sourceUrl: input));

        Assert.Equal(expected, RecipeSourceUrl.Normalize(input));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,test")]
    [InlineData("//example.com/recipe")]
    [InlineData("https://alice:secret@example.com/recipe")]
    [InlineData("https://example.com/recipe with spaces")]
    public void Validate_RejectsInvalidRecipeSourceUrls(string sourceUrl)
    {
        Assert.Throws<ArgumentException>(() => CreateRecipeCommandValidator.ValidateAndThrow(ValidCommand(sourceUrl: sourceUrl)));
    }

    [Fact]
    public void Validate_RejectsSourceUrlsThatExceedStorageLimit()
    {
        var sourceUrl = "https://example.com/" + new string('a', RecipeSourceUrl.MaxLength);

        Assert.Throws<ArgumentException>(() => CreateRecipeCommandValidator.ValidateAndThrow(ValidCommand(sourceUrl: sourceUrl)));
    }

    private static CreateRecipeCommand ValidCommand(
        string title = "Test Recipe",
        string? preparation = null,
        string? imageUrl = null,
        string? sourceUrl = null) => new()
    {
        Title = title,
        Category = 2,
        PreparationTimeMinutes = 10,
        CookingTimeMinutes = 20,
        ImageUrl = imageUrl,
        SourceUrl = sourceUrl,
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
