using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Payouts.Domain;

/// <summary>
/// ADR 0019: where an organisation is paid for sales of its courses. The bank account name must match the registered organisation
/// name. The number is stored encrypted and read only at submission time.
/// </summary>
[Audited]
public sealed class OrganisationPayoutAccount : Entity
{
    private OrganisationPayoutAccount()
    {
    }

    private OrganisationPayoutAccount(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid OrganisationId { get; private set; }
    public string BankCode { get; private set; } = string.Empty;
    public string AccountNumberCiphertext { get; private set; } = string.Empty;
    public string AccountNumberMasked { get; private set; } = string.Empty;
    public string AccountName { get; private set; } = string.Empty;
    public bool NameMatched { get; private set; }
    public DateTime VerifiedAt { get; private set; }

    public static OrganisationPayoutAccount Create(Guid tenantId, Guid organisationId, string bankCode, string accountNumberCiphertext, string accountNumberMasked, string accountName, bool nameMatched, DateTime utcNow)
        => new(NewId(), tenantId)
        {
            OrganisationId = organisationId,
            BankCode = bankCode,
            AccountNumberCiphertext = accountNumberCiphertext,
            AccountNumberMasked = accountNumberMasked,
            AccountName = accountName,
            NameMatched = nameMatched,
            VerifiedAt = utcNow,
        };

    public void Replace(string bankCode, string accountNumberCiphertext, string accountNumberMasked, string accountName, bool nameMatched, DateTime utcNow)
    {
        BankCode = bankCode;
        AccountNumberCiphertext = accountNumberCiphertext;
        AccountNumberMasked = accountNumberMasked;
        AccountName = accountName;
        NameMatched = nameMatched;
        VerifiedAt = utcNow;
    }

    /// <summary>
    /// Name match (the same token rule Tutors applies to a verified identity): every token of the registered name (ignoring order and case)
    /// appears in the bank's account name, or vice versa, allowing one token of slack.
    /// </summary>
    public static bool NamesMatch(string registeredName, string bankAccountName)
    {
        static HashSet<string> Tokens(string s) => s.ToUpperInvariant().Split([' ', ',', '-', '.'], StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length > 1).ToHashSet();
        var a = Tokens(registeredName);
        var b = Tokens(bankAccountName);
        if (a.Count == 0 || b.Count == 0)
        {
            return false;
        }

        var overlap = a.Intersect(b).Count();
        return overlap >= Math.Min(2, Math.Min(a.Count, b.Count)) && overlap >= Math.Min(a.Count, b.Count) - 1;
    }
}
