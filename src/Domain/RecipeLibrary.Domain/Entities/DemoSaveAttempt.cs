namespace RecipeLibrary.Domain.Entities;

/// <summary>A durable reservation for one logical recipe save, shared by its database retries.</summary>
public sealed class DemoSaveAttempt
{
    public Guid OperationId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid? CompletedRecipeId { get; set; }
}
