using Plastoids.SharedKernel.Results;

namespace Plastoids.Infrastructure.Integrations;

public sealed record ResolvedBankAccount(string BankCode, string AccountNumber, string AccountName);

/// <summary>Gateway "account resolve" (Paystack/Flutterwave) used for payout-account name matching (blueprint §13 stage 5).</summary>
public interface IBankAccountResolver
{
    Task<Result<ResolvedBankAccount>> ResolveAsync(string bankCode, string accountNumber, CancellationToken cancellationToken);
}

/// <summary>Fake: account number "…0000" is unknown; otherwise the name is "FAKE ACCOUNT &lt;last4&gt;" unless the number starts with "22", which yields a Nigerian test name.</summary>
public sealed class FakeBankAccountResolver : IBankAccountResolver
{
    /// <summary>Tests can pre-register (accountNumber → name) so name-matching paths are deterministic.</summary>
    public System.Collections.Concurrent.ConcurrentDictionary<string, string> KnownAccounts { get; } = new();

    public Task<Result<ResolvedBankAccount>> ResolveAsync(string bankCode, string accountNumber, CancellationToken cancellationToken)
    {
        if (accountNumber.Length != 10 || !accountNumber.All(char.IsDigit))
        {
            return Task.FromResult(Result.Failure<ResolvedBankAccount>(Error.Validation("account_number_invalid", "NUBAN account numbers have 10 digits.")));
        }

        if (accountNumber.EndsWith("0000", StringComparison.Ordinal))
        {
            return Task.FromResult(Result.Failure<ResolvedBankAccount>(Error.NotFound("account_not_found", "The bank could not resolve this account.")));
        }

        var name = KnownAccounts.TryGetValue(accountNumber, out var known) ? known : "FAKE ACCOUNT " + accountNumber[^4..];
        return Task.FromResult(Result.Success(new ResolvedBankAccount(bankCode, accountNumber, name)));
    }
}

internal sealed class PaystackBankAccountResolver(Secrets.ISecretProvider secrets) : IBankAccountResolver
{
    public Task<Result<ResolvedBankAccount>> ResolveAsync(string bankCode, string accountNumber, CancellationToken cancellationToken)
    {
        _ = secrets.GetRequired("PaystackSecretKey");
        return Task.FromResult(Result.Failure<ResolvedBankAccount>(Error.Failure("bank_resolver_not_implemented", "Paystack account-resolve is a stub.")));
    }
}
