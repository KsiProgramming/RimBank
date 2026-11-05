namespace RimBank.Architecture.Tests;

/// <summary>
/// The service domains a namespace may name, as (business domain, service domain) pairs. The solution's catalogue
/// is tech-stack §3.2: a new context adds its row here in the same PR as the ADR that checks its BIAN mapping
/// (ADR 0011).
/// </summary>
public sealed class ServiceDomainCatalogue(IEnumerable<(string BusinessDomain, string ServiceDomain)> serviceDomains)
{
    private readonly HashSet<(string, string)> _serviceDomains = [.. serviceDomains];

    /// <summary>tech-stack §3.2.</summary>
    public static ServiceDomainCatalogue Solution { get; } = new(
    [
        ("Party", "PartyReferenceDataDirectory"),
        ("ProductManagement", "ProductDirectory"),
        ("AccountManagement", "PositionKeeping"),
        ("LoansAndDeposits", "CurrentAccount"),
        ("Payments", "PaymentOrderInitiation"),
        ("Finance", "FinancialAccounting"),
    ]);

    public bool Contains(string businessDomain, string serviceDomain) => _serviceDomains.Contains((businessDomain, serviceDomain));
}
