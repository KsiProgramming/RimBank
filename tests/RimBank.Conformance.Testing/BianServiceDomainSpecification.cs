namespace RimBank.Conformance.Testing;

/// <summary>The parts of one BIAN service domain's ext-DDD specifications the harness checks against.</summary>
/// <param name="ServiceDomain">The BIAN service domain name in PascalCase, for example <c>PartyReferenceDataDirectory</c>.</param>
/// <param name="Operations">The operations of its OpenAPI specification.</param>
/// <param name="Channels">The channels of its AsyncAPI specification.</param>
public sealed record BianServiceDomainSpecification(
    string ServiceDomain,
    IReadOnlyList<BianOperation> Operations,
    IReadOnlyList<BianChannel> Channels)
{
    /// <summary>Finds an operation by its BIAN operation id.</summary>
    /// <param name="operationId">For example <c>PartyReferenceDataDirectoryEntry/Retrieve</c>.</param>
    /// <returns>The operation, or <see langword="null"/> when the specification has none with that id.</returns>
    public BianOperation? Operation(string operationId) =>
        Operations.FirstOrDefault(operation => string.Equals(operation.OperationId, operationId, StringComparison.Ordinal));
}
