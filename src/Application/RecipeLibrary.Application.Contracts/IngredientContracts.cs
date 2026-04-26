namespace RecipeLibrary.Application.Contracts;

public sealed class MatchIngredientCommand : ICommand<MatchIngredientResult>
{
    public string Input { get; init; } = string.Empty;
}

public sealed class MatchIngredientResult
{
    public string MatchType { get; init; } = "none";
    public IngredientLookupItem? Ingredient { get; init; }
    public decimal Confidence { get; init; }
    public IReadOnlyList<IngredientLookupItem> Suggestions { get; init; } = [];
}

public sealed class SearchIngredientsQuery : IQuery<IReadOnlyList<IngredientLookupItem>>
{
    public string Query { get; init; } = string.Empty;
}

public sealed class AddIngredientTagsCommand : ICommand<AddIngredientTagsResult>
{
    public Guid IngredientId { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
}

public sealed record AddIngredientTagsResult(int AddedCount);

public sealed class SearchTagsQuery : IQuery<IReadOnlyList<TagLookupItem>>
{
    public string Query { get; init; } = string.Empty;
}

public sealed class IngredientLookupItem
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class TagLookupItem
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
}
