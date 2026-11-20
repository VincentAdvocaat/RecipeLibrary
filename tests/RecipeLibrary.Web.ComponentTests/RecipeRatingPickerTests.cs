using Bunit;
using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Components.Molecules;
using RecipeLibrary.Web.Testing;
using Xunit;

namespace RecipeLibrary.Web.ComponentTests;

public sealed class RecipeRatingPickerTests : ComponentTestContext
{
    [Fact]
    public void RatingChangesOnlyAfterASuccessfulSave()
    {
        var recipeId = Guid.NewGuid();
        var bus = new RatingCommandBus();
        Services.AddSingleton<ICommandBus>(bus);
        var picker = RenderComponent<RecipeRatingPicker>(parameters => parameters
            .Add(p => p.RecipeId, recipeId)
            .Add(p => p.InitialRating, 3));

        picker.Find(Option(5)).Click();
        Assert.Equal(new SetRecipeRatingCommand(recipeId, 5), bus.LastCommand);
        Assert.Equal("true", picker.Find(Option(3)).GetAttribute("aria-pressed"));
        Assert.Equal("false", picker.Find(Option(5)).GetAttribute("aria-pressed"));
        Assert.NotNull(picker.Find("[role='alert']"));

        bus.Saved = true;
        picker.Find(Option(0)).Click();
        Assert.Equal(new SetRecipeRatingCommand(recipeId, 0), bus.LastCommand);
        Assert.Equal("true", picker.Find(Option(0)).GetAttribute("aria-pressed"));
        Assert.Empty(picker.FindAll("[role='alert']"));
    }

    private static string Option(int stars) => $"[data-testid='{UiTestIds.RecipeRatingOption(stars)}']";

    private sealed class RatingCommandBus : ICommandBus
    {
        public bool Saved { get; set; }
        public SetRecipeRatingCommand? LastCommand { get; private set; }

        public Task<TResult> SendAsync<TCommand, TResult>(TCommand command, CancellationToken ct = default)
            where TCommand : ICommand<TResult>
        {
            LastCommand = Assert.IsType<SetRecipeRatingCommand>(command);
            return Task.FromResult((TResult)(object)new SetRecipeRatingResult(Saved));
        }
    }
}
