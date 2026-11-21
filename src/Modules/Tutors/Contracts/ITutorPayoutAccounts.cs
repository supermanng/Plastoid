namespace Plastoids.Modules.Tutors.Contracts;

/// <summary>The verified payout destination. AccountNumber is decrypted for the transfer instruction only; NameMatched is the blueprint §18 "name matches the verified identity" rule.</summary>
public sealed record TutorPayoutDestination(Guid TutorUserId, string BankCode, string AccountNumber, string AccountNumberMasked, string AccountName, bool NameMatched);

public interface ITutorPayoutAccounts
{
    Task<TutorPayoutDestination?> GetAsync(Guid tutorUserId, CancellationToken cancellationToken);
}
