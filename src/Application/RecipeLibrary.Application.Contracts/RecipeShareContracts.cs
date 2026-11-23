namespace RecipeLibrary.Application.Contracts;

public sealed record GetRecipeShareLinkQuery(Guid RecipeId) : IQuery<Guid?>;

public sealed record SetRecipeShareLinkCommand(Guid RecipeId, bool Enabled) : ICommand<Guid?>;
