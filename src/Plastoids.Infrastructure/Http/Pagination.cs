namespace Plastoids.Infrastructure.Http;

/// <summary>Cursor pagination (blueprint §17): the cursor is the opaque last id of the previous page (ids are time-ordered UUID v7).</summary>
public static class Cursor
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    public static string Encode(Guid lastId) => Convert.ToBase64String(lastId.ToByteArray(bigEndian: true)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static Guid? Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        var base64 = cursor.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        try
        {
            var bytes = Convert.FromBase64String(base64);
            return bytes.Length == 16 ? new Guid(bytes, bigEndian: true) : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public static int ClampLimit(int? limit) => Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
}

public sealed record Page<T>(IReadOnlyList<T> Items, string? NextCursor);
