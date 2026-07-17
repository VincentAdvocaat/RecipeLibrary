namespace RecipeLibrary.Infrastructure.Persistence;

public sealed class PersistenceReadiness : IPersistenceReadiness
{
    private const int StateStarting = 0;
    private const int StateReady = 1;
    private const int StateFailed = 2;

    private int _state;

    public bool IsReady => Volatile.Read(ref _state) == StateReady;

    public bool HasPermanentlyFailed => Volatile.Read(ref _state) == StateFailed;

    public void MarkReady() => Interlocked.Exchange(ref _state, StateReady);

    public void MarkPermanentlyFailed()
    {
        // Ready wins: never downgrade a successful migrate/seed.
        Interlocked.CompareExchange(ref _state, StateFailed, StateStarting);
    }
}
