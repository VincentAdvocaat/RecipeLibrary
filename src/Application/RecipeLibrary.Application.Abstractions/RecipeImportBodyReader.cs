using System.Text;

namespace RecipeLibrary.Application.Abstractions;

/// <summary>
/// Reads a UTF-8 text body up to a byte limit, then stops (soft truncate) instead of failing.
/// </summary>
public static class RecipeImportBodyReader
{
    public static async Task<string> ReadUtf8UpToMaxBytesAsync(
        Stream stream,
        int maxBytes,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (maxBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytes), "MaxBytes must be positive.");
        }

        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 1024,
            leaveOpen: true);
        var buffer = new char[8192];
        var builder = new StringBuilder();
        var totalBytes = 0;

        while (totalBytes < maxBytes)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            if (read <= 0)
            {
                break;
            }

            var chunkBytes = Encoding.UTF8.GetByteCount(buffer.AsSpan(0, read));
            if (totalBytes + chunkBytes <= maxBytes)
            {
                totalBytes += chunkBytes;
                builder.Append(buffer, 0, read);
                continue;
            }

            // Soft truncate: append only characters that still fit under MaxBytes.
            var remaining = maxBytes - totalBytes;
            var take = 0;
            var used = 0;
            while (take < read)
            {
                var charBytes = Encoding.UTF8.GetByteCount(buffer.AsSpan(take, 1));
                if (used + charBytes > remaining)
                {
                    break;
                }

                used += charBytes;
                take++;
            }

            if (take > 0)
            {
                builder.Append(buffer, 0, take);
            }

            break;
        }

        return builder.ToString();
    }
}
