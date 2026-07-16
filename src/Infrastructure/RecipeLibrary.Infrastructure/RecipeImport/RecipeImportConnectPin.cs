using System.Net;

namespace RecipeLibrary.Infrastructure.RecipeImport;

/// <summary>
/// Pins the next <see cref="SocketsHttpHandler"/> connect to pre-validated public addresses
/// so a DNS rebinding between resolve and connect cannot reach private hosts.
/// </summary>
internal static class RecipeImportConnectPin
{
    private static readonly AsyncLocal<PinState?> Current = new();

    public static IDisposable Use(string host, IReadOnlyList<IPAddress> addresses)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentNullException.ThrowIfNull(addresses);
        if (addresses.Count == 0)
        {
            throw new ArgumentException("At least one address is required.", nameof(addresses));
        }

        var previous = Current.Value;
        Current.Value = new PinState(host, addresses);
        return new Restore(previous);
    }

    public static PinState? Get() => Current.Value;

    public sealed record PinState(string Host, IReadOnlyList<IPAddress> Addresses);

    private sealed class Restore(PinState? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
