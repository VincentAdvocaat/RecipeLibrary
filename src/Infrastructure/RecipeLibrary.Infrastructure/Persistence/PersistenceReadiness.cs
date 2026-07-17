namespace RecipeLibrary.Infrastructure.Persistence;

public sealed class PersistenceReadiness : IPersistenceReadiness
{
    private int _ready;

    public bool IsReady => Volatile.Read(ref _ready) == 1;

    public void MarkReady() => Interlocked.Exchange(ref _ready, 1);
}
