using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.UseCases.Ingredients;
using RecipeLibrary.Application.UseCases.RecipeImages;
using RecipeLibrary.Application.UseCases.Recipes;
using RecipeLibrary.Application.RecipeImport;
using RecipeLibrary.Application.UseCases.RecipeImport;
using RecipeLibrary.Application.UseCases.Pantry;
using RecipeLibrary.Application.UseCases.ShoppingLists;
using RecipeLibrary.Application.ShoppingLists;
using RecipeLibrary.Application.Pantry;

namespace RecipeLibrary.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<InProcessBus>();
        services.AddScoped<ICommandBus>(sp => sp.GetRequiredService<InProcessBus>());
        services.AddScoped<IQueryBus>(sp => sp.GetRequiredService<InProcessBus>());
        services.AddSingleton<IIngredientTextNormalizer, IngredientTextNormalizer>();

        services.AddScoped<ICommandHandler<CreateRecipeCommand, CreateRecipeResult>, CreateRecipeCommandHandler>();
        services.AddScoped<IQueryHandler<GetRecipeListQuery, GetRecipeListResult>, GetRecipeListQueryHandler>();
        services.AddScoped<IQueryHandler<GetRecipeByIdQuery, GetRecipeByIdResult?>, GetRecipeByIdQueryHandler>();
        services.AddScoped<IQueryHandler<GetRecipeIngredientTagsQuery, IReadOnlyList<string>>, GetRecipeIngredientTagsQueryHandler>();
        services.AddScoped<ICommandHandler<UpdateRecipeCommand, UpdateRecipeResult>, UpdateRecipeCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteRecipeCommand, DeleteRecipeResult>, DeleteRecipeCommandHandler>();
        services.AddScoped<ICommandHandler<UploadRecipeImageCommand, UploadRecipeImageResult>, UploadRecipeImageCommandHandler>();
        services.AddScoped<IQueryHandler<GetRecipeImageQuery, GetRecipeImageResult?>, GetRecipeImageQueryHandler>();
        services.AddScoped<ICommandHandler<MatchIngredientCommand, MatchIngredientResult>, MatchIngredientCommandHandler>();
        services.AddScoped<IQueryHandler<SearchIngredientsQuery, IReadOnlyList<IngredientLookupItem>>, SearchIngredientsQueryHandler>();
        services.AddScoped<ICommandHandler<AddIngredientTagsCommand, AddIngredientTagsResult>, AddIngredientTagsCommandHandler>();
        services.AddScoped<IQueryHandler<SearchTagsQuery, IReadOnlyList<TagLookupItem>>, SearchTagsQueryHandler>();
        services.AddScoped<IngredientMatcher>();
        services.AddSingleton<IngredientSimilarityScorer>();
        services.AddScoped<IngredientNameParser>();
        services.AddScoped<IngredientLineResolver>();
        services.AddScoped<ShoppingListIngredientMerger>();
        services.AddScoped<PantryIngredientMerger>();
        services.AddScoped<PantryExclusionFilter>();

        services.AddScoped<IngredientLineParser>();
        services.AddScoped<HtmlRecipeTextExtractor>();
        services.AddScoped<RecipeTextParser>();
        services.AddScoped<RecipeImportService>();
        services.AddScoped<IngredientQuantityConversionService>();
        services.AddScoped<IQueryHandler<ImportRecipeContentQuery, ImportRecipeResult>, ImportRecipeContentQueryHandler>();
        services.AddScoped<IQueryHandler<ImportRecipeFromUrlQuery, ImportRecipeResult>, ImportRecipeFromUrlQueryHandler>();
        services.AddScoped<IQueryHandler<ImportRecipeFromImageQuery, ImportRecipeResult>, ImportRecipeFromImageQueryHandler>();

        services.AddScoped<IQueryHandler<GetOrCreateShoppingListGroupQuery, GetOrCreateShoppingListGroupResult>, GetOrCreateShoppingListGroupQueryHandler>();
        services.AddScoped<IQueryHandler<GetNextShoppingListNameQuery, GetNextShoppingListNameResult>, GetNextShoppingListNameQueryHandler>();
        services.AddScoped<IQueryHandler<GetShoppingListSummaryQuery, ShoppingListSummaryResult>, GetShoppingListSummaryQueryHandler>();
        services.AddScoped<ICommandHandler<AddRecipesToShoppingListCommand, AddRecipesToShoppingListResult>, AddRecipesToShoppingListCommandHandler>();
        services.AddScoped<ICommandHandler<ToggleShoppingListItemCommand, ToggleShoppingListItemResult>, ToggleShoppingListItemCommandHandler>();
        services.AddScoped<ICommandHandler<RemoveShoppingListItemCommand, RemoveShoppingListItemResult>, RemoveShoppingListItemCommandHandler>();
        services.AddScoped<ICommandHandler<ClearShoppingListCommand, ClearShoppingListResult>, ClearShoppingListCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteShoppingListCommand, DeleteShoppingListResult>, DeleteShoppingListCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteShoppingListGroupCommand, DeleteShoppingListGroupResult>, DeleteShoppingListGroupCommandHandler>();
        services.AddScoped<ICommandHandler<SplitShoppingListCommand, SplitShoppingListResult>, SplitShoppingListCommandHandler>();
        services.AddScoped<ICommandHandler<MoveShoppingListItemCommand, MoveShoppingListItemResult>, MoveShoppingListItemCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateShoppingListNameCommand, UpdateShoppingListNameResult>, UpdateShoppingListNameCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateShoppingListItemQuantityCommand, UpdateShoppingListItemQuantityResult>, UpdateShoppingListItemQuantityCommandHandler>();
        services.AddScoped<ICommandHandler<AddManualShoppingListItemCommand, AddManualShoppingListItemResult>, AddManualShoppingListItemCommandHandler>();

        services.AddScoped<IQueryHandler<GetPantryItemsQuery, GetPantryItemsResult>, GetPantryItemsQueryHandler>();
        services.AddScoped<ICommandHandler<UpsertPantryItemCommand, UpsertPantryItemResult>, UpsertPantryItemCommandHandler>();
        services.AddScoped<ICommandHandler<RemovePantryItemCommand, RemovePantryItemResult>, RemovePantryItemCommandHandler>();
        services.AddScoped<ICommandHandler<ApplyPantryToShoppingListCommand, ApplyPantryToShoppingListResult>, ApplyPantryToShoppingListCommandHandler>();
        services.AddScoped<ICommandHandler<MoveShoppingListItemToPantryCommand, MoveShoppingListItemToPantryResult>, MoveShoppingListItemToPantryCommandHandler>();

        return services;
    }
}

