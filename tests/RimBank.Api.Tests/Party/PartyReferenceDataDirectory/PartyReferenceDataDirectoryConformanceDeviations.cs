using RimBank.Conformance.Testing;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

/// <summary>
/// The deliberate differences between the Party Reference Data Directory's bodies and its BIAN v14 schemas, each
/// decided in an ADR (ADR 0037). A difference not listed here fails the conformance tests, and so does a row that no
/// longer occurs.
/// </summary>
internal static class PartyReferenceDataDirectoryConformanceDeviations
{
    private const string EntryRegister = "PartyReferenceDataDirectoryEntry/Register";
    private const string EntryRetrieve = "PartyReferenceDataDirectoryEntry/Retrieve";
    private const string EntryUpdate = "PartyReferenceDataDirectoryEntry/Update";
    private const string DemographicsUpdate = "Demographics/Update";
    private const string DemographicsExchange = "Demographics/Exchange";
    private const string DemographicsRetrieve = "Demographics/Retrieve";
    private const string ReferenceUpdate = "Reference/Update";
    private const string ReferenceRetrieve = "Reference/Retrieve";
    private const string AssociationsRegister = "Associations/Register";
    private const string AssociationsUpdate = "Associations/Update";
    private const string AssociationsRetrieve = "Associations/Retrieve";
    private const string BankRelationsUpdate = "BankRelations/Update";
    private const string BankRelationsRetrieve = "BankRelations/Retrieve";

    private const string RecordIds =
        "ADR 0024: a response carries the ids of the records it is about, which BIAN puts only in the path, so a client can call their operations.";

    private const string Verification =
        "ADR 0028: Demographics Exchange records a verification outcome, so the record carries VerificationStatus and VerificationDate.";

    private const string ContactPoint =
        "ADR 0031: a contact point is read through its ISO 20022 subtype property inside BIAN's ContactPoint object.";

    private const string Envelope =
        "ADR 0036: a published event carries a small envelope (event id, time, entry id, stream version) so consumers can order and deduplicate.";

    public static IReadOnlyList<ConformanceDeviation> All { get; } =
    [
        // The Control Record: Register, Retrieve and Update answer with the entry and its three singleton BQs.
        .. Responses([EntryRegister, EntryRetrieve, EntryUpdate], RecordIds, "PartyReferenceDataDirectoryEntryId", "DemographicsId", "ReferenceId", "BankRelationsId"),
        .. Responses(
            [EntryRetrieve, EntryUpdate],
            "ADR 0024: the entry's last Update is returned as RefreshDate, BIAN's second DirectoryEntryDateType, next to the OpenDate pair.",
            "RefreshDate"),

        // Demographics.
        .. Responses([DemographicsUpdate, DemographicsExchange, DemographicsRetrieve], RecordIds, "PartyReferenceDataDirectoryEntryId", "DemographicsId"),
        .. Responses([DemographicsUpdate, DemographicsExchange, DemographicsRetrieve], Verification, "VerificationStatus", "VerificationDate"),
        new(DemographicsExchange, ConformancePart.Request, "$.ExchangeActionType", "ADR 0028: Exchange takes RimBank's extension field ExchangeActionType (Verify | Reject)."),

        // Reference.
        .. Responses([ReferenceUpdate, ReferenceRetrieve], RecordIds, "PartyReferenceDataDirectoryEntryId", "ReferenceId"),
        .. Both(
            ReferenceUpdate,
            [ReferenceRetrieve],
            "ADR 0030, ADR 0031: a postal address's Country is BIAN's Country object with its ISO 3166 Code, where BIAN's PostalAddress types it as a string.",
            "$.ResidentialAddress.Country"),
        .. Both(ReferenceUpdate, [ReferenceRetrieve], ContactPoint, "$.eMailAddress.EmailAddress", "$.CellPhoneNumber.MobileNumber", "$.SocialNetworkContacts.URLAddress"),
        .. Responses(
            [ReferenceUpdate, ReferenceRetrieve],
            "ADR 0034: CompanyOfficerRole is a plain role name, because BIAN types it as a Party, which has no role property.",
            "CompanyOfficerRole"),

        // Associations and BankRelations.
        .. Responses([AssociationsRegister, AssociationsUpdate, AssociationsRetrieve], RecordIds, "AssociationsId", "PartyReferenceDataDirectoryEntryId"),
        .. Responses([BankRelationsUpdate, BankRelationsRetrieve], RecordIds, "BankRelationsId", "PartyReferenceDataDirectoryEntryId"),

        // The published language (camelCase on the wire).
        .. Payloads(
            ["PartyReferenceDataDirectoryEntry/Created", "PartyReferenceDataDirectoryEntry/Updated"],
            Envelope,
            "eventId",
            "occurredAt",
            "partyReferenceDataDirectoryEntryId",
            "version"),
    ];

    private static IEnumerable<ConformanceDeviation> Responses(string[] operations, string reason, params string[] properties) =>
        operations.SelectMany(operation => properties.Select(property => new ConformanceDeviation(operation, ConformancePart.Response, "$." + property, reason)));

    // The same paths in a command's request and response, and in the responses of the operations that return its record.
    private static IEnumerable<ConformanceDeviation> Both(string command, string[] alsoReturnedBy, string reason, params string[] paths) =>
        paths.SelectMany(path => (IEnumerable<ConformanceDeviation>)
        [
            new(command, ConformancePart.Request, path, reason),
            .. alsoReturnedBy.Prepend(command).Select(operation => new ConformanceDeviation(operation, ConformancePart.Response, path, reason)),
        ]);

    private static IEnumerable<ConformanceDeviation> Payloads(string[] channels, string reason, params string[] properties) =>
        channels.SelectMany(channel => properties.Select(property => new ConformanceDeviation(channel, ConformancePart.Payload, "$." + property, reason)));
}
