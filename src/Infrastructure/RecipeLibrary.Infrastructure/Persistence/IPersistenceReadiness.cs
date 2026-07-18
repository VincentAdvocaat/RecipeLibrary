namespace RecipeLibrary.Infrastructure.Persistence;

/// <summary>
/// Tracks whether EF migrations and catalog seed have completed successfully,
/// or whether warmup has permanently failed.
/// </summary>
public interface IPersistenceReadiness
{
    PersistenceWarmupState State { get; }

    bool IsReady { get; }

    /// <summary>
    /// True when migrations will not be retried further (non-transient error or warmup deadline).
    /// </summary>
    bool HasPermanentlyFailed { get; }

    void MarkReady();

    void MarkPermanentlyFailed();
}
