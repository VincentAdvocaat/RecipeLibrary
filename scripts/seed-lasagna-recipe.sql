-- =============================================================================
-- Recipe Library: Seed script – Lasagna (ragù en bechamel)
-- =============================================================================
-- Dit recept is geïmporteerd uit een informeel recept (bron: TheBruno, 11/01/2021).
-- Een aantal hoeveelheden zijn niet in het origineel gespecificeerd en zijn
-- geschat voor gebruik in de app (zie plan: lasagna_recept_importeren).
--
-- Vereiste: Database en tabellen bestaan al (na EF Core migrations).
-- Uitvoeren: verbind met de Recipe Library-database en voer dit script uit.
-- =============================================================================

SET NOCOUNT ON;

DECLARE @RecipeId UNIQUEIDENTIFIER = NEWID();
DECLARE @Now DATETIMEOFFSET = SYSUTCDATETIME();

-- Recipe
INSERT INTO Recipes (Id, Title, Description, PreparationMinutes, CookingMinutes, Category, ImageUrl, Difficulty, Servings, CreatedAt, UpdatedAt)
VALUES (
    @RecipeId,
    N'Lasagna (ragù en bechamel)',
    N'Lasagna bestaat uit drie onderdelen: pasta, ragù en bechamel. Dit recept beschrijft de ragù en de bechamel; voor de pasta kun je kant-en-klare lasagnevellen (ei) uit de winkel gebruiken, of zelf maken.',
    60,
    210,
    2,   -- Meat
    NULL,
    0,   -- Unknown
    0,
    @Now,
    @Now
);

-- Ingredients (volgorde: ragù, bechamel, assemblage)
INSERT INTO Ingredients (Id, RecipeId, Name, Quantity, Unit) VALUES
    (NEWID(), @RecipeId, N'Gehakt', 500, N'Gram'),
    (NEWID(), @RecipeId, N'Selderij', 1, N'Piece'),
    (NEWID(), @RecipeId, N'Wortel', 1, N'Piece'),
    (NEWID(), @RecipeId, N'Ui', 1, N'Piece'),
    (NEWID(), @RecipeId, N'Verse kruiden (rozemarijn, tijm, salie) als bouquet', 1, N'Piece'),
    (NEWID(), @RecipeId, N'Rode wijn', 250, N'Milliliter'),
    (NEWID(), @RecipeId, N'Tomatensaus', 400, N'Milliliter'),
    (NEWID(), @RecipeId, N'Olie', 2, N'Tablespoon'),
    (NEWID(), @RecipeId, N'Zout', 1, N'Teaspoon'),
    (NEWID(), @RecipeId, N'Melk', 500, N'Milliliter'),
    (NEWID(), @RecipeId, N'Boter', 50, N'Gram'),
    (NEWID(), @RecipeId, N'Bloem', 50, N'Gram'),
    (NEWID(), @RecipeId, N'Nootmuskaat', 1, N'Teaspoon'),
    (NEWID(), @RecipeId, N'Lasagnevellen (ei)', 1, N'Piece'),
    (NEWID(), @RecipeId, N'Geraspte kaas', 100, N'Gram');

-- Instruction steps (Nederlands, gebaseerd op het originele recept)
INSERT INTO InstructionSteps (Id, RecipeId, StepNumber, Text) VALUES
    (NEWID(), @RecipeId, 1,
        N'Lasagna bestaat uit drie onderdelen: de pasta, de ragù en de bechamel. Voor de pasta kun je kant-en-klare lasagnevellen (ei) in de winkel kopen; je kunt ook zelf pasta maken, maar dat duurt langer. Hieronder volgen de ragù en de bechamel.'),

    (NEWID(), @RecipeId, 2,
        N'Ragù – Voorbereiding: Snijd de selderij, wortel en ui fijn (met mes of mixer). Dit is de basis (soffritto) voor de ragù.'),

    (NEWID(), @RecipeId, 3,
        N'Ragù – Verhit een pot met een beetje olie. Voeg het mengsel van selderij, wortel en ui toe en bak op laag vuur. Voeg een beetje zout toe zodat het vocht vrijkomt. Bak tot het geheel een goudgele, graanachtige kleur heeft.'),

    (NEWID(), @RecipeId, 4,
        N'Ragù – Voeg het gehakt toe en zet het vuur hoger. Roer kort zodat de groenten niet aanbranden. Roer daarna niet te veel: laat het vlees een korst vormen zoals bij gegrild vlees; dat geeft smaak. Schep daarna om en bak het vlees overal gaar.'),

    (NEWID(), @RecipeId, 5,
        N'Ragù – Giet de rode wijn erbij (ongeveer tot het vlees bijna onder staat). Blijf roeren tot het weer kookt. Laat 3 tot 5 minuten met de wijn inkoken.'),

    (NEWID(), @RecipeId, 6,
        N'Ragù – Voeg de tomatensaus toe; die moet al het vlees bedekken en er iets bovenuit komen. Zet het vuur laag. Bind de rozemarijn, tijm en salie met een touwtje tot een bouquet en leg die in de saus. Laat de ragù minstens 3 uur pruttelen; hoe langer, hoe beter (binnen rede). Zet het vuur uit en laat de saus buiten de koelkast afkoelen. Gebruik de saus niet gloeiend heet voor de lasagna.'),

    (NEWID(), @RecipeId, 7,
        N'Bechamel – Breng de melk in een pot aan de kook. Voeg zout en een beetje nootmuskaat toe (voor de geur, niet overdrijven).'),

    (NEWID(), @RecipeId, 8,
        N'Bechamel – Smelt in een andere pot van vergelijkbare grootte de boter. Laat de boter niet verbranden. Voeg de bloem toe en meng goed; je krijgt een egale, doffe gele massa (roux). Houd het op laag vuur. Zodra de melk bijna kookt, giet je alle melk bij de boter-bloemmix. Zet het vuur hoger en roer goed met een garde tot alles is gebonden. Blijf roeren tot de bechamel dikker wordt (als dikke melk); dat duurt ongeveer 10 minuten of iets minder. Laat daarna afkoelen.'),

    (NEWID(), @RecipeId, 9,
        N'Assemblage – Haal het kruidenbouquet uit de ragù. Neem een ovenschaal en leg lagen in deze volgorde: bechamel, pasta, ragù. Herhaal (bechamel, pasta, ragù) en houd de hoeveelheid saus per laag in balans. Maak minstens 8 tot 10 lagen in totaal. Na de laatste laag: verdeel de rest van de saus over de bovenkant en strooi er royaal geraspte kaas over.'),

    (NEWID(), @RecipeId, 10,
        N'Verwarm de oven voor op 180 °C. Bak de lasagna in 25 tot 30 minuten gaar. Houd in de gaten: niet elke oven werkt hetzelfde, dus controleer of het goed is. Daarna is de lasagna klaar.');

SELECT @RecipeId AS InsertedRecipeId;
