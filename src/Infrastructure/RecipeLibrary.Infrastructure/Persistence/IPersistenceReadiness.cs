namespace RecipeLibrary.Infrastructure.Persistence;

/// <summary>
/// Tracks whether EF migrations and catalog seed have completed successfully.
/// </summary>
public interface IPersistenceReadiness
{
    bool IsReady { get; }

    void MarkReady();
}
