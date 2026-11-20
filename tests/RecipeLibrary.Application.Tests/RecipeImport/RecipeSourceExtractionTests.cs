using RecipeLibrary.Application.RecipeImport;
using Xunit;

namespace RecipeLibrary.Application.Tests.RecipeImport;

public sealed class RecipeSourceExtractionTests
{
    [Fact]
    public void TextExtraction_PreservesNumberedMeasuredAndBareIngredients()
    {
        var draft = RecipeTextDocumentExtractor.Extract("Ingredients\n1. 200 g flour\n2. 100 g sugar\n3. 2 large eggs\n4. olive oil\n5. salt and pepper");

        Assert.Equal(["200 g flour", "100 g sugar", "2 large eggs", "olive oil", "salt and pepper"], draft.IngredientLines);
        Assert.Empty(draft.Steps);
    }

    [Fact]
    public void HtmlExtraction_FillsMissingVisibleStepsAndPreservesSchemaFields()
    {
        const string html = """
            <script type="application/ld+json">{"@type":"Recipe","name":"Carrot soup","description":"A warming soup.","prepTime":"PT5M","recipeYield":"2 servings","recipeIngredient":["2 carrots"]}</script>
            <main><h1>Carrot soup</h1><h2>Method</h2><p>Simmer the carrots for 8 minutes.</p></main>
            """;

        var draft = RecipeTextDocumentExtractor.Extract(new HtmlRecipeTextExtractor().Extract(html));

        Assert.Equal(["2 carrots"], draft.IngredientLines);
        Assert.Equal("Simmer the carrots for 8 minutes.", Assert.Single(draft.Steps).Text);
        Assert.Equal("A warming soup.", draft.Description);
        Assert.Equal(5, draft.PreparationTimeMinutes);
        Assert.Equal(2, draft.Servings);
    }

    [Fact]
    public void HtmlExtraction_FillsMissingVisibleIngredientsWithoutDuplicatingSchemaSteps()
    {
        const string html = """
            <script type="application/ld+json">{"@type":"Recipe","name":"Carrot soup","recipeInstructions":["Simmer the carrots for 8 minutes."]}</script>
            <main><h1>Carrot soup</h1><h2>Ingredients</h2><p>2 carrots</p>
            <h2>Method</h2><p>Boil the carrots for 10 minutes.</p></main>
            """;

        var draft = RecipeTextDocumentExtractor.Extract(new HtmlRecipeTextExtractor().Extract(html));

        Assert.Equal(["2 carrots"], draft.IngredientLines);
        Assert.Equal("Simmer the carrots for 8 minutes.", Assert.Single(draft.Steps).Text);
    }

    [Fact]
    public void HtmlExtraction_PrefersCompleteRecipeAndRejectsVolumeAsServingCount()
    {
        var html = """
            <script type="application/ld+json">{"@type":"Recipe","name":"Preview","recipeIngredient":["50 g oil"]}</script>
            <script type="application/ld+json">
            {"@graph":[{"@type":["Thing","Recipe"],"name":"Lemon dressing","recipeYield":["50","50 ml"],"totalTime":"PT2H",
            "recipeIngredient":["50 ml olive oil","1 lemon"],
            "recipeInstructions":[{"@type":"HowToSection","itemListElement":[{"@type":"HowToStep","text":"Whisk oil &amp; lemon juice."}]}]}]}
            </script>
            """;

        var draft = RecipeTextDocumentExtractor.Extract(new HtmlRecipeTextExtractor().Extract(html));

        Assert.Equal("Lemon dressing", draft.Title);
        Assert.Equal(["50 ml olive oil", "1 lemon"], draft.IngredientLines);
        Assert.Equal("Whisk oil & lemon juice.", Assert.Single(draft.Steps).Text);
        Assert.Null(draft.Servings);
        Assert.Null(draft.CookingTimeMinutes);
    }

    [Fact]
    public void HtmlExtraction_UsesVisibleRecipeSectionsWhenSchemaIsPartial()
    {
        var html = """
            <script type="application/ld+json">{"@type":"Recipe","name":"Preview","recipeIngredient":["50 g oil"]}</script>
            <main><aside>Suggested recipes</aside><article><h1>Skillet beans</h1>
            <h2>Ingredients</h2><ul><li>1.5 cups beans</li><li>2 cloves garlic</li></ul>
            <p>1. Fry the garlic.<br>2. Stir in the beans and heat through.</p>
            <footer>Subscribe now</footer></article></main>
            """;

        var draft = RecipeTextDocumentExtractor.Extract(new HtmlRecipeTextExtractor().Extract(html));

        Assert.Equal("Skillet beans", draft.Title);
        Assert.Equal(["1.5 cups beans", "2 cloves garlic"], draft.IngredientLines);
        Assert.Equal(["Fry the garlic.", "Stir in the beans and heat through."], draft.Steps.Select(step => step.Text));
    }

    [Fact]
    public void FullAiInput_PreservesUnrecognizedInstructions()
    {
        const string source = "Fricassée d'oignons\n4 oignons\nFaire revenir les oignons puis ajouter le bouillon.";

        Assert.Equal(source, RecipeTextDocumentExtractor.NormalizePlainTextForAi(source));
    }
}
