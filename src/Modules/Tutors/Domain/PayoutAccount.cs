using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Tutors.Domain;

/// <summary>Blueprint §13 stage 5: the bank account name must match the KYC-verified identity. The number is stored encrypted.</summary>
[Audited]
public sealed class PayoutAccount : Entity
{
    private PayoutAccount()
    {
    }

    private PayoutAccount(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid TutorProfileId { get; private set; }
    public string BankCode { get; private set; } = string.Empty;
    public string AccountNumberCiphertext { get; private set; } = string.Empty;
    public string AccountNumberMasked { get; private set; } = string.Empty;
    public string AccountName { get; private set; } = string.Empty;
    public bool NameMatched { get; private set; }
    public DateTime VerifiedAt { get; private set; }

    public static PayoutAccount Create(Guid tenantId, Guid tutorProfileId, string bankCode, string accountNumberCiphertext, string accountNumberMasked, string accountName, bool nameMatched, DateTime utcNow)
        => new(NewId(), tenantId)
        {
            TutorProfileId = tutorProfileId,
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

    /// <summary>Name match: every token of the verified name (ignoring order and case) appears in the bank's account name, or vice versa.</summary>
    public static bool NamesMatch(string verifiedFullName, string bankAccountName)
    {
        static HashSet<string> Tokens(string s) => s.ToUpperInvariant().Split([' ', ',', '-', '.'], StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length > 1).ToHashSet();
        var a = Tokens(verifiedFullName);
        var b = Tokens(bankAccountName);
        if (a.Count == 0 || b.Count == 0)
        {
            return false;
        }

        var overlap = a.Intersect(b).Count();
        return overlap >= Math.Min(2, Math.Min(a.Count, b.Count)) && overlap >= Math.Min(a.Count, b.Count) - 1;
    }
}
