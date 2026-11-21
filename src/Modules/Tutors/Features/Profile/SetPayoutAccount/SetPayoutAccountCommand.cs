using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Tutors.Features.Profile.SetPayoutAccount;

public sealed record SetPayoutAccountCommand(string BankCode, string AccountNumber) : ICommand<SetPayoutAccountResponse>;

public sealed record SetPayoutAccountResponse(string BankCode, string AccountNumberMasked, string AccountName, bool NameMatched);
