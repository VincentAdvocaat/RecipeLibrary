using Bunit;
using RecipeLibrary.Components.Molecules;
using Xunit;

namespace RecipeLibrary.Web.ComponentTests;

public sealed class ConfirmPanelTests : ComponentTestContext
{
    [Fact]
    public async Task ClickYes_InvokesOnYes()
    {
        var yesCalled = false;
        var cut = RenderComponent<ConfirmPanel>(parameters => parameters
            .Add(p => p.Message, "Sure?")
            .Add(p => p.OnYes, EventCallback.Factory.Create(this, () => { yesCalled = true; return Task.CompletedTask; }))
            .Add(p => p.OnNo, EventCallback.Factory.Create(this, () => Task.CompletedTask)));

        cut.Find("[data-testid='confirm-yes']").Click();

        Assert.True(yesCalled);
    }

    [Fact]
    public async Task ClickNo_InvokesOnNo()
    {
        var noCalled = false;
        var cut = RenderComponent<ConfirmPanel>(parameters => parameters
            .Add(p => p.Message, "Sure?")
            .Add(p => p.OnYes, EventCallback.Factory.Create(this, () => Task.CompletedTask))
            .Add(p => p.OnNo, EventCallback.Factory.Create(this, () => { noCalled = true; return Task.CompletedTask; })));

        cut.Find("[data-testid='confirm-no']").Click();

        Assert.True(noCalled);
    }
}
