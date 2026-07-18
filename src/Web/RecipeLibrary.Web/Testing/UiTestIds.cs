namespace RecipeLibrary.Web.Testing;

/// <summary>Stable data-testid values for UI and E2E tests (language-independent).</summary>
public static class UiTestIds
{
    public const string NavRecipes = "nav-recipes";
    public const string NavCreate = "nav-create";
    public const string NavShoppingList = "nav-shopping-list";
    public const string NavBadge = "nav-badge";

    public const string SearchInput = "search-input";
    public const string CategoryAll = "category-all";
    public const string CategoryMeat = "category-meat";
    public const string ViewToggleList = "view-toggle-list";
    public const string ViewToggleCards = "view-toggle-cards";
    public const string SelectModeStart = "select-mode-start";
    public const string AddSelectedToList = "add-selected-to-list";

    public const string RecipeTitle = "recipe-title";
    public const string RecipeDescription = "recipe-description";
    public const string RecipeSave = "recipe-save";
    public const string RecipeCancel = "recipe-cancel";
    public const string IngredientAddRow = "ingredient-add-row";
    public const string StepAdd = "step-add";
    public const string TagInput = "tag-input";
    public const string TagAdd = "tag-add";

    public const string RecipeEdit = "recipe-edit";
    public const string RecipeDelete = "recipe-delete";
    public const string DeleteConfirmYes = "delete-confirm-yes";
    public const string DeleteConfirmNo = "delete-confirm-no";
    public const string AddToShoppingList = "add-to-shopping-list";

    public const string ListRename = "list-rename";
    public const string ListRenameSave = "list-rename-save";
    public const string SplitStart = "split-start";
    public const string SplitCreateFromSelection = "split-create-from-selection";
    public const string SplitConfirm = "split-confirm";
    public const string ListClear = "list-clear";
    public const string ListDelete = "list-delete";
    public const string ConfirmYes = "confirm-yes";
    public const string ConfirmNo = "confirm-no";

    public const string LanguageSwitcher = "language-switcher";
    public const string LanguageEn = "language-en";
    public const string LanguageNl = "language-nl";

    public const string MeasureSystemSwitcher = "measure-system-switcher";
    public const string MeasureSystemMetric = "measure-system-metric";
    public const string MeasureSystemImperial = "measure-system-imperial";

    public const string IngredientConvert = "ingredient-convert";
    public const string IngredientConvertEstimate = "ingredient-convert-estimate";

    public const string OverviewReady = "overview-ready";
    public const string ShoppingListReady = "shopping-list-ready";

    public static string RecipeCard(Guid recipeId) => $"recipe-card-{recipeId:D}";
    public static string RecipeListItem(Guid recipeId) => $"recipe-list-item-{recipeId:D}";
    public static string SelectRecipe(Guid recipeId) => $"select-recipe-{recipeId:D}";
    public static string IngredientRowName(int index) => $"ingredient-row-{index}-name";
    public static string IngredientRowPreparation(int index) => $"ingredient-row-{index}-preparation";
    public static string IngredientRowRemove(int index) => $"ingredient-row-{index}-remove";
    public static string StepInput(int index) => $"step-{index}";
    public static string ListTab(Guid listId) => $"list-tab-{listId:D}";
    public static string ItemCheckbox(Guid itemId) => $"item-{itemId:D}-checkbox";
    public static string ItemMove(Guid itemId) => $"item-{itemId:D}-move";
    public static string ItemRemove(Guid itemId) => $"item-{itemId:D}-remove";
    public static string ItemQuantityEdit(Guid itemId) => $"item-{itemId:D}-quantity-edit";
    public static string ItemQuantityInput(Guid itemId) => $"item-{itemId:D}-quantity-input";
    public static string ItemQuantitySave(Guid itemId) => $"item-{itemId:D}-quantity-save";

    public const string AddItemForm = "add-item-form";
    public const string AddItemName = "add-item-name";
    public const string AddItemPreparation = "add-item-preparation";
    public const string AddItemSubmit = "add-item-submit";
    public const string ApplyPantry = "apply-pantry";

    public const string PantryReady = "pantry-ready";
    public const string PantryAddForm = "pantry-add-form";
    public const string PantryAddName = "pantry-add-name";
    public const string PantryAddSubmit = "pantry-add-submit";
    public const string NavPantry = "nav-pantry";

    public static string PantryItemRemove(Guid itemId) => $"pantry-{itemId:D}-remove";

    public static string ItemMoveToPantry(Guid itemId) => $"item-{itemId:D}-move-to-pantry";
}
