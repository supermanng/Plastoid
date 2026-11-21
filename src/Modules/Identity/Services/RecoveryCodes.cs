using System.Security.Cryptography;
using System.Text;

namespace Plastoids.Modules.Identity.Services;

/// <summary>Ten 10-character recovery codes (xxxxx-xxxxx), shown once; only hashes are stored.</summary>
internal static class RecoveryCodes
{
    private const string Alphabet = "abcdefghjkmnpqrstuvwxyz23456789";
    public const int Count = 10;

    public static IReadOnlyList<string> Generate()
    {
        var codes = new List<string>(Count);
        for (var i = 0; i < Count; i++)
        {
            var chars = new char[10];
            for (var j = 0; j < chars.Length; j++)
            {
                chars[j] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            }

            codes.Add(new string(chars, 0, 5) + "-" + new string(chars, 5, 5));
        }

        return codes;
    }

    public static string Hash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(code.Trim().ToLowerInvariant())));
}
