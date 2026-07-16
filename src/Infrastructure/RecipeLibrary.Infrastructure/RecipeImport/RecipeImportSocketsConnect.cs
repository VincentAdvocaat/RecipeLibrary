using System.Net;
using System.Net.Sockets;
using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Infrastructure.RecipeImport;

internal static class RecipeImportSocketsConnect
{
    public static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var endPoint = context.DnsEndPoint;
        var candidates = await ResolveCandidatesAsync(endPoint, cancellationToken);

        Exception? lastError = null;
        foreach (var address in candidates)
        {
            if (RecipeImportUrlSafety.IsBlockedAddress(address))
            {
                continue;
            }

            Socket? socket = null;
            try
            {
                socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true,
                };

                await socket.ConnectAsync(new IPEndPoint(address, endPoint.Port), cancellationToken)
                    .ConfigureAwait(false);

                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException or OperationCanceledException)
            {
                socket?.Dispose();
                lastError = ex;
                if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
            }
        }

        throw new HttpRequestException(
            "Unable to connect to a safe address for the URL host.",
            lastError);
    }

    private static async Task<IReadOnlyList<IPAddress>> ResolveCandidatesAsync(
        DnsEndPoint endPoint,
        CancellationToken cancellationToken)
    {
        var pin = RecipeImportConnectPin.Get();
        if (pin is not null
            && string.Equals(pin.Host, endPoint.Host, StringComparison.OrdinalIgnoreCase))
        {
            return pin.Addresses;
        }

        if (IPAddress.TryParse(endPoint.Host, out var literal))
        {
            if (RecipeImportUrlSafety.IsBlockedAddress(literal))
            {
                throw new HttpRequestException("URL host is not allowed.");
            }

            return [literal];
        }

        IPAddress[] resolved;
        try
        {
            resolved = await Dns.GetHostAddressesAsync(endPoint.Host, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SocketException ex)
        {
            throw new HttpRequestException("URL host could not be resolved.", ex);
        }

        if (resolved.Length == 0 || resolved.Any(RecipeImportUrlSafety.IsBlockedAddress))
        {
            throw new HttpRequestException("URL host is not allowed.");
        }

        return resolved;
    }
}
