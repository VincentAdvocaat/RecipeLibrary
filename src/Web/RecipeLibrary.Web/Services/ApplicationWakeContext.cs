using System.Threading;

namespace RecipeLibrary.Web.Services;

/// <summary>Carries the explicit start attempt through asynchronous initialization and SQL callbacks.</summary>
public static class ApplicationWakeContext
{
    private static readonly AsyncLocal<ApplicationWakeEvent?> State = new();
    public static ApplicationWakeEvent? Current => State.Value;

    public static IDisposable Push(ApplicationWakeEvent attempt)
    {
        var previous = State.Value;
        State.Value = attempt;
        return new Restore(previous);
    }

    private sealed class Restore(ApplicationWakeEvent? previous) : IDisposable
    {
        public void Dispose() => State.Value = previous;
    }
}
