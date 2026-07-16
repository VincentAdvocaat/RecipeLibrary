using RecipeLibrary.Application.RecipeImport;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests.RecipeImport;

public sealed class RecipeTextDocumentExtractorTests
{
    [Fact]
    public void Extract_NoisyPage_SkipsChrome_KeepsIngredientsAndStopsAtFooter()
    {
        const string text = """
            Ga naar de inhoud
            Naar voorbeeld op facebook
            Heerlijke tomatensoep is een klassieker voor koude dagen. Serveer met vers brood.
            20 M
            De moeilijkheidsgraad van dit receptuur is: gemiddeld.
            Extra marketing tekst die na de tijd komt en genegeerd moet worden voor de beschrijving.
            Ingrediënten: Tomatensoep
            Markeer als gereed
            400 g tomaten
            Markeer als gereed
            1 teen knoflook
            peper en zout
            Merknamen in de ingrediëntenlijst kunnen affiliate links zijn.
            Kookstand aanzetten
            albert-heijnjumbo
            Bereiding: Tomatensoep
            Snijd de tomaten in stukken en fruit de knoflook.
            Voeg water toe en laat 20 minuten koken.
            Soep stap 1Soep stap 2Soep stap 3
            Tips
            Dit is tiptekst die geen stap mag worden.
            Beoordelingen
            """;

        var doc = RecipeTextDocumentExtractor.Extract(text);

        Assert.Equal("Tomatensoep", doc.Title);
        Assert.Equal(20, doc.CookingTimeMinutes);
        Assert.Equal((int)Difficulty.Medium, doc.Difficulty);
        Assert.StartsWith("Heerlijke tomatensoep", doc.Description);
        Assert.DoesNotContain("Extra marketing", doc.Description);
        Assert.Equal(3, doc.IngredientLines.Count);
        Assert.Contains(doc.IngredientLines, x => x.Contains("tomaten", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(doc.IngredientLines, x => x.Contains("knoflook", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(doc.IngredientLines, x => x.Contains("peper", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(doc.IngredientLines, x => x.Contains("albert", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, doc.Steps.Count);
        Assert.DoesNotContain(doc.Steps, s => s.Text.Contains("Tips", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(doc.Steps, s => s.Text.Contains("stap 1", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Extract_CleanLabeledDocument_ReadsServingsAndDifficulty()
    {
        const string text = """
            Inleiding:
            Korte omschrijving van het recept.

            15 M
            Makkelijk
            4 porties

            Ingrediënten: Testgerecht
            2 eieren
            zout

            Bereiding: Testgerecht
            Klop de eieren luchtig op.
            """;

        var doc = RecipeTextDocumentExtractor.Extract(text);

        Assert.Equal("Testgerecht", doc.Title);
        Assert.Equal(15, doc.CookingTimeMinutes);
        Assert.Equal((int)Difficulty.Easy, doc.Difficulty);
        Assert.Equal(4, doc.Servings);
        Assert.Equal(2, doc.IngredientLines.Count);
        Assert.Single(doc.Steps);
    }

    [Fact]
    public void Extract_DoesNotTreatInstructionStartingWithGaNaarOvenAsChrome()
    {
        const string text = """
            Inleiding:
            Korte omschrijving.

            Ingrediënten: Toast
            2 sneetjes brood

            Bereiding: Toast
            Ga naar de oven en rooster het brood knapperig.
            Bestuif met zout.
            """;

        var doc = RecipeTextDocumentExtractor.Extract(text);

        Assert.Equal(2, doc.Steps.Count);
        Assert.Contains(doc.Steps, s => s.Text.Contains("Ga naar de oven", StringComparison.Ordinal));
    }

    [Fact]
    public void Extract_LabeledPrepAndCookTimes_AreSplit()
    {
        const string text = """
            Inleiding:
            Korte omschrijving.

            Bereidingstijd: 10 M
            Kooktijd: 20 M
            Makkelijk

            Ingrediënten: Soep
            400 g tomaten

            Bereiding: Soep
            Kook de tomaten gaar.
            """;

        var doc = RecipeTextDocumentExtractor.Extract(text);

        Assert.Equal(10, doc.PreparationTimeMinutes);
        Assert.Equal(20, doc.CookingTimeMinutes);
    }
}
