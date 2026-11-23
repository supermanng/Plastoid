using System.Text.RegularExpressions;

namespace Plastoids.Modules.Sponsorship.Services;

/// <summary>
/// Normalises the contact a sponsor types when allocating a seat (ADR 0021) the same way Identity does, so the blind index
/// matches the account's: Nigerian numbers to E.164 (+234…), emails trimmed and lower-cased. Sponsorship stores the hash
/// and a masked form only, never the number.
/// </summary>
internal static partial class ContactNormaliser
{
    public static bool TryNormalisePhone(string? input, out string e164)
    {
        e164 = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var digits = NonDigits().Replace(input, string.Empty);
        if (input.TrimStart().StartsWith('+'))
        {
            e164 = "+" + digits;
        }
        else if (digits.Length == 11 && digits[0] == '0')
        {
            e164 = "+234" + digits[1..];
        }
        else if (digits.Length == 13 && digits.StartsWith("234", StringComparison.Ordinal))
        {
            e164 = "+" + digits;
        }
        else
        {
            return false;
        }

        return E164().IsMatch(e164);
    }

    /// <summary>+234803****67: enough for the sponsor to recognise who they named.</summary>
    public static string MaskPhone(string e164) => e164.Length <= 6 ? e164 : e164[..^6] + "****" + e164[^2..];

    public static string? NormaliseEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var trimmed = email.Trim().ToLowerInvariant();
        return trimmed.Contains('@') && trimmed.Length <= 254 ? trimmed : null;
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigits();

    [GeneratedRegex(@"^\+[1-9]\d{9,14}$")]
    private static partial Regex E164();
}
