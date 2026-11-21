using System.Security.Cryptography;
using System.Text;

namespace Plastoids.Infrastructure.Security;

/// <summary>RFC 6238 time-based one-time passwords (SHA-1, 6 digits, 30 s step) compatible with authenticator apps.</summary>
public static class Totp
{
    public const int Digits = 6;
    public const int StepSeconds = 30;

    public static byte[] GenerateSecret() => RandomNumberGenerator.GetBytes(20);

    public static string Compute(byte[] secret, DateTime utcNow, long stepOffset = 0)
    {
        var counter = (long)Math.Floor((utcNow - DateTime.UnixEpoch).TotalSeconds / StepSeconds) + stepOffset;
        Span<byte> counterBytes = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);
#pragma warning disable CA5350 // RFC 6238 / authenticator apps mandate HMAC-SHA1 for TOTP
        var hash = HMACSHA1.HashData(secret, counterBytes);
#pragma warning restore CA5350
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Accepts the current step and one step either side to absorb clock drift.</summary>
    public static bool Verify(byte[] secret, string code, DateTime utcNow)
    {
        if (code.Length != Digits)
        {
            return false;
        }

        for (var offset = -1; offset <= 1; offset++)
        {
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Compute(secret, utcNow, offset)), Encoding.ASCII.GetBytes(code)))
            {
                return true;
            }
        }

        return false;
    }

    public static string OtpAuthUri(string issuer, string account, byte[] secret)
        => $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}?secret={Base32.Encode(secret)}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits={Digits}&period={StepSeconds}";
}

/// <summary>RFC 4648 Base32 without padding, as used by authenticator apps.</summary>
public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(ReadOnlySpan<byte> data)
    {
        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                sb.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
        {
            sb.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        }

        return sb.ToString();
    }

    public static byte[] Decode(string text)
    {
        var clean = text.TrimEnd('=').ToUpperInvariant();
        var output = new List<byte>(clean.Length * 5 / 8);
        var buffer = 0;
        var bits = 0;
        foreach (var c in clean)
        {
            var value = Alphabet.IndexOf(c, StringComparison.Ordinal);
            if (value < 0)
            {
                throw new FormatException("Invalid Base32 character.");
            }

            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }

        return [.. output];
    }
}
