-- =============================================================================
-- Recipe Library: Seed script – Lasagna (ragù en bechamel)
-- =============================================================================
-- Schema: Recipes, RecipeIngredients (per-recipe lines), Ingredients (canonical).
-- Run after EF Core migrations (including BackfillCanonicalIngredientNames).
-- =============================================================================

SET NOCOUNT ON;

DECLARE @RecipeId UNIQUEIDENTIFIER = NEWID();
DECLARE @Now DATETIMEOFFSET = SYSUTCDATETIME();

INSERT INTO Recipes (Id, Title, Description, PreparationMinutes, CookingMinutes, Category, ImageUrl, Difficulty, Servings, CreatedAt, UpdatedAt)
VALUES (
    @RecipeId,
    N'Lasagna (ragù en bechamel)',
    N'Lasagna bestaat uit drie onderdelen: pasta, ragù en bechamel. Dit recept beschrijft de ragù en de bechamel; voor de pasta kun je kant-en-klare lasagnevellen (ei) uit de winkel gebruiken, of zelf maken.',
    60,
    210,
    2,
    NULL,
    0,
    0,
    @Now,
    @Now
);

-- Helper: insert canonical ingredient (if missing) and recipe line
DECLARE @CanonicalId UNIQUEIDENTIFIER;
DECLARE @LineId UNIQUEIDENTIFIER;
DECLARE @Norm NVARCHAR(200);
DECLARE @Name NVARCHAR(200);

-- Gehakt 500g
SET @Name = N'Gehakt';
SET @Norm = N'gehakt';
SET @CanonicalId = (SELECT Id FROM Ingredients WHERE NormalizedName = @Norm);
IF @CanonicalId IS NULL
BEGIN
    SET @CanonicalId = NEWID();
    INSERT INTO Ingredients (Id, CanonicalName, NormalizedName, CreatedAt) VALUES (@CanonicalId, @Name, @Norm, @Now);
END
INSERT INTO RecipeIngredients (Id, RecipeId, IngredientId, Name, Preparation, Quantity, Unit)
VALUES (NEWID(), @RecipeId, @CanonicalId, @Name, NULL, 500, N'Gram');

-- Selderij
SET @Name = N'Selderij'; SET @Norm = N'selderij';
SET @CanonicalId = (SELECT Id FROM Ingredients WHERE NormalizedName = @Norm);
IF @CanonicalId IS NULL BEGIN SET @CanonicalId = NEWID(); INSERT INTO Ingredients (Id, CanonicalName, NormalizedName, CreatedAt) VALUES (@CanonicalId, @Name, @Norm, @Now); END
INSERT INTO RecipeIngredients (Id, RecipeId, IngredientId, Name, Preparation, Quantity, Unit) VALUES (NEWID(), @RecipeId, @CanonicalId, @Name, NULL, 1, N'Piece');

-- Wortel
SET @Name = N'Wortel'; SET @Norm = N'wortel';
SET @CanonicalId = (SELECT Id FROM Ingredients WHERE NormalizedName = @Norm);
IF @CanonicalId IS NULL BEGIN SET @CanonicalId = NEWID(); INSERT INTO Ingredients (Id, CanonicalName, NormalizedName, CreatedAt) VALUES (@CanonicalId, @Name, @Norm, @Now); END
INSERT INTO RecipeIngredients (Id, RecipeId, IngredientId, Name, Preparation, Quantity, Unit) VALUES (NEWID(), @RecipeId, @CanonicalId, @Name, NULL, 1, N'Piece');

-- Ui
SET @Name = N'Ui'; SET @Norm = N'ui';
SET @CanonicalId = (SELECT Id FROM Ingredients WHERE NormalizedName = @Norm);
IF @CanonicalId IS NULL BEGIN SET @CanonicalId = NEWID(); INSERT INTO Ingredients (Id, CanonicalName, NormalizedName, CreatedAt) VALUES (@CanonicalId, @Name, @Norm, @Now); END
INSERT INTO RecipeIngredients (Id, RecipeId, IngredientId, Name, Preparation, Quantity, Unit) VALUES (NEWID(), @RecipeId, @CanonicalId, @Name, NULL, 1, N'Piece');

-- Verse kruiden
SET @Name = N'Verse kruiden (rozemarijn, tijm, salie) als bouquet'; SET @Norm = N'verse kruiden (rozemarijn, tijm, salie) als bouquet';
SET @CanonicalId = (SELECT Id FROM Ingredients WHERE NormalizedName = @Norm);
IF @CanonicalId IS NULL BEGIN SET @CanonicalId = NEWID(); INSERT INTO Ingredients (Id, CanonicalName, NormalizedName, CreatedAt) VALUES (@CanonicalId, @Name, @Norm, @Now); END
INSERT INTO RecipeIngredients (Id, RecipeId, IngredientId, Name, Preparation, Quantity, Unit) VALUES (NEWID(), @RecipeId, @CanonicalId, @Name, NULL, 1, N'Piece');

-- Rode wijn
SET @Name = N'Rode wijn'; SET @Norm = N'rode wijn';
SET @CanonicalId = (SELECT Id FROM Ingredients WHERE NormalizedName = @Norm);
IF @CanonicalId IS NULL BEGIN SET @CanonicalId = NEWID(); INSERT INTO Ingredients (Id, CanonicalName, NormalizedName, CreatedAt) VALUES (@CanonicalId, @Name, @Norm, @Now); END
INSERT INTO RecipeIngredients (Id, RecipeId, IngredientId, Name, Preparation, Quantity, Unit) VALUES (NEWID(), @RecipeId, @CanonicalId, @Name, NULL, 250, N'Milliliter');

-- Tomatensaus
SET @Name = N'Tomatensaus'; SET @Norm = N'tomatensaus';
SET @CanonicalId = (SELECT Id FROM Ingredients WHERE NormalizedName = @Norm);
IF @CanonicalId IS NULL BEGIN SET @CanonicalId = NEWID(); INSERT INTO Ingredients (Id, CanonicalName, NormalizedName, CreatedAt) VALUES (@CanonicalId, @Name, @Norm, @Now); END
INSERT INTO RecipeIngredients (Id, RecipeId, IngredientId, Name, Preparation, Quantity, Unit) VALUES (NEWID(), @RecipeId, @CanonicalId, @Name, NULL, 400, N'Milliliter');

-- Olie
SET @Name = N'Olie'; SET @Norm = N'olie';
SET @CanonicalId = (SELECT Id FROM Ingredients WHERE NormalizedName = @Norm);
IF @CanonicalId IS NULL BEGIN SET @CanonicalId = NEWID(); INSERT INTO Ingredients (Id, CanonicalName, NormalizedName, CreatedAt) VALUES (@CanonicalId, @Name, @Norm, @Now); END
INSERT INTO RecipeIngredients (Id, RecipeId, IngredientId, Name, Preparation, Quantity, Unit) VALUES (NEWID(), @RecipeId, @CanonicalId, @Name, NULL, 2, N'Tablespoon');

-- Zout
SET @Name = N'Zout'; SET @Norm = N'zout';
SET @CanonicalId = (SELECT Id FROM Ingredients WHERE NormalizedName = @Norm);
IF @CanonicalId IS NULL BEGIN SET @CanonicalId = NEWID(); INSERT INTO Ingredients (Id, CanonicalName, NormalizedName, CreatedAt) VALUES (@CanonicalId, @Name, @Norm, @Now); END
INSERT INTO RecipeIngredients (Id, RecipeId, IngredientId, Name, Preparation, Quantity, Unit) VALUES (NEWID(), @RecipeId, @CanonicalId, @Name, NULL, 1, N'Teaspoon');

-- Melk
SET @Name = N'Melk'; SET @Norm = N'melk';
SET @CanonicalId = (SELECT Id FROM Ingredients WHERE NormalizedName = @Norm);
IF @CanonicalId IS NULL BEGIN SET @CanonicalId = NEWID(); INSERT INTO Ingredients (Id, CanonicalName, NormalizedName, CreatedAt) VALUES (@CanonicalId, @Name, @Norm, @Now); END
INSERT INTO RecipeIngredients (Id, RecipeId, IngredientId, Name, Preparation, Quantity, Unit) VALUES (NEWID(), @RecipeId, @CanonicalId, @Name, NULL, 500, N'Milliliter');

-- Boter
SET @Name = N'Boter'; SET @Norm = N'boter';
SET @CanonicalId = (SELECT Id FROM Ingredients WHERE NormalizedName = @Norm);
IF @CanonicalId IS NULL BEGIN SET @CanonicalId = NEWID(); INSERT INTO Ingredients (Id, CanonicalName, NormalizedName, CreatedAt) VALUES (@CanonicalId, @Name, @Norm, @Now); END
INSERT INTO RecipeIngredients (Id, RecipeId, IngredientId, Name, Preparation, Quantity, Unit) VALUES (NEWID(), @RecipeId, @CanonicalId, @Name, NULL, 50, N'Gram');

-- Bloem
SET @Name = N'Bloem'; SET @Norm = N'bloem';
SET @CanonicalId = (SELECT Id FROM Ingredients WHERE NormalizedName = @Norm);
IF @CanonicalId IS NULL BEGIN SET @CanonicalId = NEWID(); INSERT INTO Ingredients (Id, CanonicalName, NormalizedName, CreatedAt) VALUES (@CanonicalId, @Name, @Norm, @Now); END
INSERT INTO RecipeIngredients (Id, RecipeId, IngredientId, Name, Preparation, Quantity, Unit) VALUES (NEWID(), @RecipeId, @CanonicalId, @Name, NULL, 50, N'Gram');

-- Nootmuskaat
SET @Name = N'Nootmuskaat'; SET @Norm = N'nootmuskaat';
SET @CanonicalId = (SELECT Id FROM Ingredients WHERE NormalizedName = @Norm);
IF @CanonicalId IS NULL BEGIN SET @CanonicalId = NEWID(); INSERT INTO Ingredients (Id, CanonicalName, NormalizedName, CreatedAt) VALUES (@CanonicalId, @Name, @Norm, @Now); END
INSERT INTO RecipeIngredients (Id, RecipeId, IngredientId, Name, Preparation, Quantity, Unit) VALUES (NEWID(), @RecipeId, @CanonicalId, @Name, NULL, 1, N'Teaspoon');

-- Lasagnevellen
SET @Name = N'Lasagnevellen (ei)'; SET @Norm = N'lasagnevellen (ei)';
SET @CanonicalId = (SELECT Id FROM Ingredients WHERE NormalizedName = @Norm);
IF @CanonicalId IS NULL BEGIN SET @CanonicalId = NEWID(); INSERT INTO Ingredients (Id, CanonicalName, NormalizedName, CreatedAt) VALUES (@CanonicalId, @Name, @Norm, @Now); END
INSERT INTO RecipeIngredients (Id, RecipeId, IngredientId, Name, Preparation, Quantity, Unit) VALUES (NEWID(), @RecipeId, @CanonicalId, @Name, NULL, 1, N'Piece');

-- Geraspte kaas
SET @Name = N'Geraspte kaas'; SET @Norm = N'geraspte kaas';
SET @CanonicalId = (SELECT Id FROM Ingredients WHERE NormalizedName = @Norm);
IF @CanonicalId IS NULL BEGIN SET @CanonicalId = NEWID(); INSERT INTO Ingredients (Id, CanonicalName, NormalizedName, CreatedAt) VALUES (@CanonicalId, @Name, @Norm, @Now); END
INSERT INTO RecipeIngredients (Id, RecipeId, IngredientId, Name, Preparation, Quantity, Unit) VALUES (NEWID(), @RecipeId, @CanonicalId, @Name, NULL, 100, N'Gram');

INSERT INTO InstructionSteps (Id, RecipeId, StepNumber, Text) VALUES
    (NEWID(), @RecipeId, 1, N'Lasagna bestaat uit drie onderdelen: de pasta, de ragù en de bechamel. Voor de pasta kun je kant-en-klare lasagnevellen (ei) in de winkel kopen; je kunt ook zelf pasta maken, maar dat duurt langer. Hieronder volgen de ragù en de bechamel.'),
    (NEWID(), @RecipeId, 2, N'Ragù – Voorbereiding: Snijd de selderij, wortel en ui fijn (met mes of mixer). Dit is de basis (soffritto) voor de ragù.'),
    (NEWID(), @RecipeId, 3, N'Ragù – Verhit een pot met een beetje olie. Voeg het mengsel van selderij, wortel en ui toe en bak op laag vuur. Voeg een beetje zout toe zodat het vocht vrijkomt. Bak tot het geheel een goudgele, graanachtige kleur heeft.'),
    (NEWID(), @RecipeId, 4, N'Ragù – Voeg het gehakt toe en zet het vuur hoger. Roer kort zodat de groenten niet aanbranden. Roer daarna niet te veel: laat het vlees een korst vormen zoals bij gegrild vlees; dat geeft smaak. Schep daarna om en bak het vlees overal gaar.'),
    (NEWID(), @RecipeId, 5, N'Ragù – Giet de rode wijn erbij (ongeveer tot het vlees bijna onder staat). Blijf roeren tot het weer kookt. Laat 3 tot 5 minuten met de wijn inkoken.'),
    (NEWID(), @RecipeId, 6, N'Ragù – Voeg de tomatensaus toe; die moet al het vlees bedekken en er iets bovenuit komen. Zet het vuur laag. Bind de rozemarijn, tijm en salie met een touwtje tot een bouquet en leg die in de saus. Laat de ragù minstens 3 uur pruttelen; hoe langer, hoe beter (binnen rede). Zet het vuur uit en laat de saus buiten de koelkast afkoelen. Gebruik de saus niet gloeiend heet voor de lasagna.'),
    (NEWID(), @RecipeId, 7, N'Bechamel – Breng de melk in een pot aan de kook. Voeg zout en een beetje nootmuskaat toe (voor de geur, niet overdrijven).'),
    (NEWID(), @RecipeId, 8, N'Bechamel – Smelt in een andere pot van vergelijkbare grootte de boter. Laat de boter niet verbranden. Voeg de bloem toe en meng goed; je krijgt een egale, doffe gele massa (roux). Houd het op laag vuur. Zodra de melk bijna kookt, giet je alle melk bij de boter-bloemmix. Zet het vuur hoger en roer goed met een garde tot alles is gebonden. Blijf roeren tot de bechamel dikker wordt (als dikke melk); dat duurt ongeveer 10 minuten of iets minder. Laat daarna afkoelen.'),
    (NEWID(), @RecipeId, 9, N'Assemblage – Haal het kruidenbouquet uit de ragù. Neem een ovenschaal en leg lagen in deze volgorde: bechamel, pasta, ragù. Herhaal (bechamel, pasta, ragù) en houd de hoeveelheid saus per laag in balans. Maak minstens 8 tot 10 lagen in totaal. Na de laatste laag: verdeel de rest van de saus over de bovenkant en strooi er royaal geraspte kaas over.'),
    (NEWID(), @RecipeId, 10, N'Verwarm de oven voor op 180 °C. Bak de lasagna in 25 tot 30 minuten gaar. Houd in de gaten: niet elke oven werkt hetzelfde, dus controleer of het goed is. Daarna is de lasagna klaar.');

SELECT @RecipeId AS InsertedRecipeId;
