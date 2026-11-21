using System.Text.RegularExpressions;

namespace Plastoids.Modules.Identity.Domain;

/// <summary>E.164 normalisation. Nigerian local numbers (0803…) become +234803…; anything else must already carry a country code.</summary>
public static partial class PhoneNumber
{
    public static bool TryNormalise(string? input, out string e164)
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

    /// <summary>+234803****67 — enough for the user to recognise their own number.</summary>
    public static string Mask(string e164)
        => e164.Length <= 6 ? e164 : e164[..^6] + "****" + e164[^2..];

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigits();

    [GeneratedRegex(@"^\+[1-9]\d{9,14}$")]
    private static partial Regex E164();
}
