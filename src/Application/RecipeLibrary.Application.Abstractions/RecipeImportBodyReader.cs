using System.Text;

namespace RecipeLibrary.Application.Abstractions;

/// <summary>
/// Reads a UTF-8 text body up to a byte limit, then stops (soft truncate) instead of failing.
/// </summary>
public static class RecipeImportBodyReader
{
    public static async Task<RecipeImportBodyReadResult> ReadUtf8UpToMaxBytesAsync(
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
        var wasTruncated = false;

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

            // Soft truncate: append only complete UTF-16 units that still fit under MaxBytes.
            var remaining = maxBytes - totalBytes;
            var take = 0;
            var used = 0;
            while (take < read)
            {
                var unitLength = GetUtf16UnitLength(buffer, take, read);
                if (unitLength == 0)
                {
                    break;
                }

                var charBytes = Encoding.UTF8.GetByteCount(buffer.AsSpan(take, unitLength));
                if (used + charBytes > remaining)
                {
                    break;
                }

                used += charBytes;
                take += unitLength;
            }

            if (take > 0)
            {
                builder.Append(buffer, 0, take);
                totalBytes += used;
            }

            wasTruncated = true;
            break;
        }

        // Exact MaxBytes fill via full chunks does not enter the soft-truncate path;
        // peek so we still report truncation when more content remains.
        if (!wasTruncated && totalBytes >= maxBytes)
        {
            var peek = await reader.ReadAsync(buffer.AsMemory(0, 1), ct);
            if (peek > 0)
            {
                wasTruncated = true;
            }
        }

        return new RecipeImportBodyReadResult(builder.ToString(), wasTruncated);
    }

    /// <summary>
    /// Returns 2 for a surrogate pair, 1 for a BMP char, or 0 when a high surrogate lacks its pair in-buffer.
    /// </summary>
    private static int GetUtf16UnitLength(char[] buffer, int index, int length)
    {
        var c = buffer[index];
        if (!char.IsHighSurrogate(c))
        {
            return 1;
        }

        if (index + 1 < length && char.IsLowSurrogate(buffer[index + 1]))
        {
            return 2;
        }

        return 0;
    }
}

public readonly record struct RecipeImportBodyReadResult(string Text, bool WasTruncated);
