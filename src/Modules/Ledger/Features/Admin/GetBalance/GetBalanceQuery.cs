using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Ledger.Features.Admin.GetBalance;

public sealed record GetBalanceQuery(string AccountCode, Guid? OwnerId) : IQuery<BalanceResponse>;

public sealed record BalanceResponse(string AccountCode, Guid? OwnerId, Money Balance);
