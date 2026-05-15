-- Verwijdert alle lasagna-recepten (oud + nieuw schema).
-- Gebruik dit als de app verwijderen niet lukt.

SET NOCOUNT ON;

DECLARE @RecipeId UNIQUEIDENTIFIER;
DECLARE recipe_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT Id FROM Recipes WHERE Title LIKE N'%Lasagna%';

OPEN recipe_cursor;
FETCH NEXT FROM recipe_cursor INTO @RecipeId;

WHILE @@FETCH_STATUS = 0
BEGIN
    DELETE FROM InstructionSteps WHERE RecipeId = @RecipeId;
    DELETE FROM RecipeIngredients WHERE RecipeId = @RecipeId;
    DELETE FROM Recipes WHERE Id = @RecipeId;
    FETCH NEXT FROM recipe_cursor INTO @RecipeId;
END

CLOSE recipe_cursor;
DEALLOCATE recipe_cursor;
