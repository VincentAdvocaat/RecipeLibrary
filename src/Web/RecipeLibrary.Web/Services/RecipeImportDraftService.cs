using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Web.Services;

public sealed class RecipeImportDraftService
{
    public ImportRecipeResult? Draft { get; private set; }

    public void SetDraft(ImportRecipeResult draft) => Draft = draft;

    public bool TryConsumeDraft(out ImportRecipeResult? draft)
    {
        draft = Draft;
        Draft = null;
        return draft is not null;
    }
}
