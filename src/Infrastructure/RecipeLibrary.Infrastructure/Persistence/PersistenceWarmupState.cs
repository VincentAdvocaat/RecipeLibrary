namespace RecipeLibrary.Infrastructure.Persistence;

/// <summary>
/// Warmup lifecycle for EF migrations and catalog seed.
/// </summary>
public enum PersistenceWarmupState
{
    Starting = 0,
    Ready = 1,
    Failed = 2
}
