using System.Collections;
using System.Reflection;
using Serilog.Core;
using Serilog.Events;

namespace Plastoids.Infrastructure.Observability;

/// <summary>
/// Build prompt Sprint 9 / blueprint §19: PII never reaches logs. Any object logged with the @ destructuring operator has
/// its sensitive members replaced by "***" (phone, email, NIN, BVN, DOB, account numbers, secrets, tokens, names of
/// people). Dictionaries are masked by key the same way. Strings logged directly are not inspected: never log raw PII.
/// </summary>
public sealed class PiiDestructuringPolicy : IDestructuringPolicy
{
    public const string Mask = "***";

    private static readonly string[] SensitiveFragments =
    [
        "phone", "email", "nin", "bvn", "dob", "dateofbirth", "birth", "password", "secret", "token", "otp", "accountnumber", "cardnumber", "pan", "cvv",
        "address", "fullname", "firstname", "lastname", "surname", "displayname", "learnername", "accountname", "signatory", "selfie", "ciphertext", "hash", "ip",
    ];

    /// <summary>Short fragments (nin, bvn, dob, pan, cvv, ip) must match a whole camel-case/underscore token; longer ones match as substrings.</summary>
    public static bool IsSensitive(string name)
    {
        var tokens = Tokens(name);
        var joined = string.Concat(tokens);
        return SensitiveFragments.Any(f => f.Length <= 3 ? tokens.Contains(f) : joined.Contains(f, StringComparison.Ordinal));
    }

    private static List<string> Tokens(string name)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var ch = name[i];
            if (ch == '_' || ch == '-' || ch == ' ')
            {
                Flush();
                continue;
            }

            if (char.IsUpper(ch) && current.Length > 0 && !char.IsUpper(name[i - 1]))
            {
                Flush();
            }

            current.Append(char.ToLowerInvariant(ch));
        }

        Flush();
        return tokens;

        void Flush()
        {
            if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }
    }

    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue result)
    {
        result = null!;
        if (value is string || value is ValueType || value is Delegate || value is Type)
        {
            return false;
        }

        if (value is IDictionary dictionary)
        {
            var elements = new List<KeyValuePair<ScalarValue, LogEventPropertyValue>>();
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = entry.Key?.ToString() ?? string.Empty;
                elements.Add(new KeyValuePair<ScalarValue, LogEventPropertyValue>(new ScalarValue(key), IsSensitive(key) ? new ScalarValue(Mask) : propertyValueFactory.CreatePropertyValue(entry.Value, true)));
            }

            result = new DictionaryValue(elements);
            return true;
        }

        if (value is IEnumerable)
        {
            return false; // sequences are destructured element by element, each element passing through this policy
        }

        var type = value.GetType();
        var properties = new List<LogEventProperty>();
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
        {
            object? propertyValue;
            try
            {
                propertyValue = property.GetValue(value);
            }
            catch (TargetInvocationException)
            {
                continue;
            }

            properties.Add(new LogEventProperty(property.Name, IsSensitive(property.Name) ? new ScalarValue(Mask) : propertyValueFactory.CreatePropertyValue(propertyValue, true)));
        }

        result = new StructureValue(properties, type.Name);
        return true;
    }
}
