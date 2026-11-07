using RimBank.Conformance.Testing;

namespace RimBank.Conformance.Tests;

/// <summary>One BIAN operation of a service domain, as an ADR maps it.</summary>
/// <param name="OperationId">The BIAN operation id, for example <c>Associations/Register</c>.</param>
/// <param name="Kind">Whether RimBank implements it as a command or a query.</param>
/// <param name="Phase">The first roadmap phase that implements it (<c>1.2</c> for Register, built over 1.2–1.5), or <see cref="PartyReferenceDataDirectoryCatalogue.Deferred"/>.</param>
public sealed record CataloguedOperation(string OperationId, BianOperationKind Kind, string Phase);

/// <summary>One BIAN AsyncAPI channel of a service domain, as an ADR maps it.</summary>
/// <param name="Address">The channel address, for example <c>PartyReferenceDataDirectoryEntry/Created</c>.</param>
/// <param name="Phase">The roadmap phase that publishes it, or <see cref="PartyReferenceDataDirectoryCatalogue.Deferred"/>.</param>
public sealed record CataloguedChannel(string Address, string Phase);

// Roadmap 1.1: the operation and channel tables of ADR 0024 (docs/adr/0024-party-reference-data-directory-mapping.md)
// as data, pinned to the real BIAN v14 specifications by PartyReferenceDataDirectoryCatalogueTests.
// A phase that changes the mapping updates the ADR and this table in the same pull request.
public static class PartyReferenceDataDirectoryCatalogue
{
    /// <summary>The BIAN service domain name.</summary>
    public const string ServiceDomain = "PartyReferenceDataDirectory";

    /// <summary>The phase of an operation or channel that Stage 1 does not implement.</summary>
    public const string Deferred = "deferred";

    /// <summary>Every operation of the OpenAPI specification.</summary>
    public static IReadOnlyList<CataloguedOperation> Operations { get; } =
    [
        new("PartyReferenceDataDirectoryEntry/Register", BianOperationKind.Command, "1.2"),
        new("PartyReferenceDataDirectoryEntry/Retrieve", BianOperationKind.Query, "1.6"),
        new("PartyReferenceDataDirectoryEntry/Update", BianOperationKind.Command, "1.8"),
        new("Demographics/Update", BianOperationKind.Command, "1.9"),
        new("Demographics/Exchange", BianOperationKind.Command, "1.9"),
        new("Demographics/Retrieve", BianOperationKind.Query, "1.9"),
        new("Reference/Update", BianOperationKind.Command, "1.10"),
        new("Reference/Retrieve", BianOperationKind.Query, "1.10"),
        new("Associations/Register", BianOperationKind.Command, "1.11"),
        new("Associations/Update", BianOperationKind.Command, "1.11"),
        new("Associations/Retrieve", BianOperationKind.Query, "1.11"),
        new("BankRelations/Update", BianOperationKind.Command, "1.12"),
        new("BankRelations/Retrieve", BianOperationKind.Query, "1.12"),
        new("PartyReferenceDataDirectoryEntry/Control", BianOperationKind.Command, Deferred),
        new("PartyReferenceDataDirectoryEntry/Request", BianOperationKind.Command, Deferred),
        new("PartyReferenceDataDirectoryEntry/Execute", BianOperationKind.Command, Deferred),
        new("PartyReferenceDataDirectoryEntry/Exchange", BianOperationKind.Command, Deferred),
    ];

    /// <summary>Every channel of the AsyncAPI specification.</summary>
    public static IReadOnlyList<CataloguedChannel> Channels { get; } =
    [
        new("PartyReferenceDataDirectoryEntry/Created", "1.13"),
        new("PartyReferenceDataDirectoryEntry/Updated", "1.13"),
        new("Demographics/Created", Deferred),
        new("Demographics/Updated", Deferred),
        new("Reference/Created", Deferred),
        new("Reference/Updated", Deferred),
        new("Associations/Created", Deferred),
        new("Associations/Updated", Deferred),
        new("BankRelations/Created", Deferred),
        new("BankRelations/Updated", Deferred),
    ];
}
