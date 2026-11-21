using Plastoids.SharedKernel.Domain;

namespace Plastoids.Infrastructure.Integrations;

public sealed record SettledTransaction(string GatewayReference, Money Gross, Money Fee, DateTime SettledAt);

/// <summary>Gateway settlement report (blueprint §18): what the gateway paid into client funds and what is still in transit.</summary>
public interface IGatewaySettlementReport
{
    Task<IReadOnlyList<SettledTransaction>> GetSettlementsAsync(DateOnly upTo, IReadOnlyCollection<string> candidateReferences, CancellationToken cancellationToken);
    Task<Money?> GetInTransitAsync(DateOnly asOf, CancellationToken cancellationToken);
}

/// <summary>Bank statement of the segregated client-funds account.</summary>
public interface IBankStatement
{
    Task<Money?> GetClientFundsBalanceAsync(DateOnly asOf, CancellationToken cancellationToken);
}

/// <summary>Fake report: every candidate reference settles the next day with Paystack-style fees; "in transit" mirrors the ledger unless overridden by tests.</summary>
public sealed class FakeGatewaySettlementReport : IGatewaySettlementReport
{
    public Money? InTransitOverride { get; set; }

    public Task<IReadOnlyList<SettledTransaction>> GetSettlementsAsync(DateOnly upTo, IReadOnlyCollection<string> candidateReferences, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<SettledTransaction>>(candidateReferences.Select(r => new SettledTransaction(r, Money.Zero(), Money.Zero(), upTo.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))).ToList());

    public Task<Money?> GetInTransitAsync(DateOnly asOf, CancellationToken cancellationToken) => Task.FromResult(InTransitOverride);
}

/// <summary>Fake statement: null means "trust the ledger's 1020 balance"; tests set an override to simulate a shortfall.</summary>
public sealed class FakeBankStatement : IBankStatement
{
    public Money? BalanceOverride { get; set; }

    public Task<Money?> GetClientFundsBalanceAsync(DateOnly asOf, CancellationToken cancellationToken) => Task.FromResult(BalanceOverride);
}
