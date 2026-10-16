using Microsoft.EntityFrameworkCore;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using RecipeLibrary.Infrastructure.Persistence;

namespace RecipeLibrary.Web.Demo;

/// <summary>Original sample recipes. Photos and their provenance are bundled under images/demo.</summary>
public static class DemoSeed
{
    public const int Version = 1;
    public static bool IsImage(string image) => Enumerable.Range(1, 8).Any(i => image == $"/images/demo/recipe-{i}.png");

    public static async Task PopulateAsync(RecipeDbContext db, string owner, DateTimeOffset now, CancellationToken ct)
    {
        var definitions = new (string Title, string Description, RecipeCategory Category, int Prep, int Cook, int Servings, Difficulty Difficulty, string Ingredients, string Steps)[]
        {
            ("Spaghetti met tomaat en basilicum", "Een eenvoudige pastamaaltijd met een frisse tomatensaus.", RecipeCategory.Vegetarian, 10, 20, 2, Difficulty.Easy,
                "spaghetti|200|Gram;tomaat|400|Gram;ui|1|Piece;knoflook|2|Clove;olijfolie|2|Tablespoon;Parmezaanse kaas|40|Gram;basilicum|10|Gram",
                "Snipper de ui en hak de knoflook. Snijd de tomaten in blokjes.;Fruit ui en knoflook in olijfolie. Voeg de tomaten toe en laat 15 minuten zacht koken.;Kook de spaghetti volgens de verpakking. Meng met de saus en serveer met basilicum en kaas."),
            ("Couscous met geroosterde groenten", "Kleurrijke couscous met kikkererwten, paprika en courgette.", RecipeCategory.Vegan, 15, 25, 4, Difficulty.Easy,
                "couscous|250|Gram;kikkererwten|400|Gram;paprika|2|Piece;courgette|1|Piece;ui|1|Piece;olijfolie|2|Tablespoon",
                "Verwarm de oven voor op 200 graden. Snijd de groenten in stukken.;Meng de groenten en uitgelekte kikkererwten met olie. Rooster ze 25 minuten.;Bereid de couscous volgens de verpakking. Schep de groenten erdoor en breng op smaak."),
            ("Romige paddenstoelenrisotto", "Rustig roeren levert een zachte risotto met gebakken champignons op.", RecipeCategory.Vegetarian, 15, 35, 3, Difficulty.Medium,
                "risottorijst|250|Gram;champignon|300|Gram;ui|1|Piece;groentebouillon|900|Milliliter;boter|30|Gram;Parmezaanse kaas|60|Gram",
                "Snipper de ui en snijd de champignons. Houd de bouillon warm.;Bak de champignons in de helft van de boter en zet apart.;Fruit de ui in de rest van de boter. Bak de rijst kort mee en voeg steeds een soeplepel bouillon toe.;Blijf roeren tot de rijst gaar is. Meng champignons en kaas erdoor."),
            ("Citroenkip met aardappeltjes", "Een ovenschotel met kip, citroen en sperziebonen.", RecipeCategory.Meat, 15, 40, 4, Difficulty.Easy,
                "kipfilet|600|Gram;aardappel|800|Gram;sperzieboon|400|Gram;citroen|1|Piece;knoflook|2|Clove;olijfolie|3|Tablespoon",
                "Verwarm de oven voor op 200 graden. Halveer de aardappels en snijd de citroen in parten.;Leg de kip en aardappels in een ovenschaal. Verdeel olie, knoflook en citroen erover.;Bak ongeveer 40 minuten en controleer of de kip helemaal gaar is.;Kook de sperziebonen in 8 minuten beetgaar en serveer erbij."),
            ("Gehaktballetjes in tomatensaus", "Kleine rundergehaktballetjes met rijst en kruidige saus.", RecipeCategory.Meat, 25, 30, 4, Difficulty.Medium,
                "rundergehakt|500|Gram;rijst|300|Gram;tomaat|600|Gram;ui|1|Piece;ei|1|Piece;paneermeel|40|Gram;olijfolie|2|Tablespoon",
                "Meng gehakt, ei en paneermeel. Breng op smaak en draai kleine balletjes.;Bak de balletjes rondom bruin in de olie. Voeg gesnipperde ui en gesneden tomaten toe.;Laat 25 minuten zacht garen tot het gehakt helemaal gaar is.;Kook de rijst volgens de verpakking en serveer met de saus."),
            ("Linzen-curry met spinazie", "Een plantaardige curry die in één pan kan pruttelen.", RecipeCategory.Vegan, 10, 30, 4, Difficulty.Easy,
                "rode linzen|250|Gram;spinazie|300|Gram;kokosmelk|400|Milliliter;tomaat|300|Gram;ui|1|Piece;kerriepoeder|2|Teaspoon;olijfolie|1|Tablespoon",
                "Spoel de linzen. Snipper de ui en snijd de tomaten.;Fruit de ui in de olie en bak de kerrie kort mee.;Voeg linzen, tomaten, kokosmelk en 300 ml water toe. Laat 25 minuten zacht koken.;Roer de spinazie erdoor tot deze slinkt. Voeg water toe als de curry te dik is."),
            ("Geroosterde pompoensoep", "Een zachte soep met geroosterde pompoen en gember.", RecipeCategory.Vegan, 20, 45, 6, Difficulty.Medium,
                "pompoen|1200|Gram;wortel|300|Gram;ui|2|Piece;gember|20|Gram;groentebouillon|1000|Milliliter;olijfolie|2|Tablespoon",
                "Verwarm de oven voor op 200 graden. Verwijder de pompoenpitten en snijd pompoen en wortel in stukken.;Meng met de olie en rooster 30 minuten.;Fruit de ui en fijngehakte gember in een soeppan. Voeg de geroosterde groenten en bouillon toe.;Laat 15 minuten koken en pureer voorzichtig tot een gladde soep."),
            ("Yoghurt met granola en bessen", "Een snel ontbijt met knapperige havermout en vers fruit.", RecipeCategory.Vegetarian, 5, 10, 2, Difficulty.Easy,
                "yoghurt|400|Gram;havermout|80|Gram;blauwe bes|100|Gram;framboos|100|Gram;honing|1|Tablespoon",
                "Rooster de havermout in een droge koekenpan op laag vuur tot deze licht kleurt.;Laat iets afkoelen en meng met de honing.;Verdeel de yoghurt over twee kommen en voeg de granola en gewassen bessen toe.")
        };

        var catalog = await db.IngredientTranslations.Where(x => x.LanguageCode == "nl")
            .Select(x => new { x.DisplayName, x.IngredientId }).ToListAsync(ct);
        Guid? Find(string name) => catalog.FirstOrDefault(x => string.Equals(x.DisplayName, name, StringComparison.OrdinalIgnoreCase))?.IngredientId;
        var recipes = definitions.Select((d, index) =>
        {
            var recipe = new Recipe
            {
                Id = Guid.NewGuid(), OwnerUserId = owner, Title = new RecipeTitle(d.Title), Description = d.Description,
                Category = d.Category, PreparationMinutes = d.Prep, CookingMinutes = d.Cook,
                Servings = d.Servings, Difficulty = d.Difficulty, CreatedAt = now, UpdatedAt = now,
                ImageUrl = $"/images/demo/recipe-{index + 1}.png"
            };
            foreach (var line in d.Ingredients.Split(';'))
            {
                var parts = line.Split('|');
                recipe.Ingredients.Add(new Ingredient
                {
                    Id = Guid.NewGuid(), RecipeId = recipe.Id, Name = parts[0], IngredientId = Find(parts[0]),
                    Quantity = new Quantity(decimal.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture)),
                    Unit = Enum.Parse<Unit>(parts[2])
                });
            }
            foreach (var (text, i) in d.Steps.Split(';').Select((text, i) => (text, i)))
                recipe.InstructionSteps.Add(new InstructionStep { Id = Guid.NewGuid(), RecipeId = recipe.Id, StepNumber = i + 1, Text = text });
            return recipe;
        }).ToList();
        db.Recipes.AddRange(recipes);

        var group = new ShoppingListGroup { Id = Guid.NewGuid(), OwnerUserId = owner, CreatedAt = now, UpdatedAt = now };
        var list = new ShoppingList { Id = Guid.NewGuid(), GroupId = group.Id, Name = "Boodschappen voor deze week", StoreOrder = 1, CreatedAt = now, UpdatedAt = now };
        foreach (var ingredient in recipes[0].Ingredients)
        {
            var item = new ShoppingListItem
            {
                Id = Guid.NewGuid(), ShoppingListId = list.Id, DisplayName = ingredient.Name,
                CanonicalIngredientId = ingredient.IngredientId, Quantity = ingredient.Quantity,
                Unit = ingredient.Unit, SortOrder = list.Items.Count
            };
            item.Sources.Add(new ShoppingListItemSource { ShoppingListItemId = item.Id, RecipeId = recipes[0].Id, RecipeTitle = recipes[0].Title.Value });
            list.Items.Add(item);
        }
        group.Lists.Add(list);
        db.ShoppingListGroups.Add(group);
        foreach (var name in new[] { "olijfolie", "ui", "knoflook" })
            db.PantryItems.Add(new PantryItem { Id = Guid.NewGuid(), OwnerUserId = owner, DisplayName = name, CanonicalIngredientId = Find(name), CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync(ct);
    }
}
