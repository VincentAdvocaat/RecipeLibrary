using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.UseCases.Ingredients;

public sealed class AddIngredientTagsCommandHandler(IIngredientRepository ingredientRepository, IIngredientTextNormalizer normalizer)
    : ICommandHandler<AddIngredientTagsCommand, AddIngredientTagsResult>
{
    public async Task<AddIngredientTagsResult> HandleAsync(AddIngredientTagsCommand command, CancellationToken ct = default)
    {
        var cleaned = command.Tags
            .Select(x => (Name: (x ?? string.Empty).Trim(), NormalizedName: normalizer.Normalize(x)))
            .Where(x => x.Name.Length > 0 && x.NormalizedName.Length > 0)
            .DistinctBy(x => x.NormalizedName)
            .ToList();

        await ingredientRepository.AddTagsAsync(command.IngredientId, cleaned, ct);
        return new AddIngredientTagsResult(cleaned.Count);
    }
}
