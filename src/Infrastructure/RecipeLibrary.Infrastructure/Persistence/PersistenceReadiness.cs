namespace RecipeLibrary.Infrastructure.Persistence;

public sealed class PersistenceReadiness : IPersistenceReadiness
{
    private int _state;

    public PersistenceWarmupState State => (PersistenceWarmupState)Volatile.Read(ref _state);

    public bool IsReady => State == PersistenceWarmupState.Ready;

    public bool HasPermanentlyFailed => State == PersistenceWarmupState.Failed;

    public void MarkReady() => Interlocked.Exchange(ref _state, (int)PersistenceWarmupState.Ready);

    public void MarkPermanentlyFailed()
    {
        // Ready wins: never downgrade a successful migrate/seed.
        Interlocked.CompareExchange(
            ref _state,
            (int)PersistenceWarmupState.Failed,
            (int)PersistenceWarmupState.Starting);
    }
}
