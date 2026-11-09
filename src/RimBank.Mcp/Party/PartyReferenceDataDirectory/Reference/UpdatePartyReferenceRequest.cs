using System.Text.Json;
using System.Text.Json.Serialization;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// The <c>request</c> argument of <c>party_reference_data_directory_update_reference</c>, mirroring the HTTP request
/// (ADR 0039, ADR 0040): BIAN's <c>UpdateReference</c> with its property names (ADR 0024, ADR 0029), as a partial update
/// of the Reference. A value left out keeps its value, and an empty one (or an object whose parts are all empty) clears it. Parts of BIAN objects
/// RimBank does not record are refused; properties that are not BIAN's are ignored.
/// </summary>
public sealed record UpdatePartyReferenceRequest
{
    /// <summary>BIAN <c>GovernmentIssuedIdentityReference</c>: the tax identification and national registration numbers.</summary>
    [JsonPropertyName("GovernmentIssuedIdentityReference")]
    public GovernmentIssuedIdentityModel? GovernmentIssuedIdentityReference { get; init; }

    /// <summary>BIAN <c>PartyLegalEntityReference</c>: the LEI in its <c>Identification</c>.</summary>
    [JsonPropertyName("PartyLegalEntityReference")]
    public PartyLegalEntityModel? PartyLegalEntityReference { get; init; }

    /// <summary>BIAN <c>PartyFullLegalName</c> (Max140Text).</summary>
    [JsonPropertyName("PartyFullLegalName")]
    public string? PartyFullLegalName { get; init; }

    /// <summary>BIAN <c>PartyNameSalutation</c> (Max35Text).</summary>
    [JsonPropertyName("PartyNameSalutation")]
    public string? PartyNameSalutation { get; init; }

    /// <summary>BIAN <c>DateofBirth</c>: an ISO 8601 date, or a date-time whose date is kept as written.</summary>
    [JsonPropertyName("DateofBirth")]
    public string? DateofBirth { get; init; }

    /// <summary>BIAN <c>Nationality</c>: the ISO 3166 code in its <c>Code</c>.</summary>
    [JsonPropertyName("Nationality")]
    public CountryModel? Nationality { get; init; }

    /// <summary>BIAN <c>PartyCountryOfResidence</c>: the ISO 3166 code in its <c>Code</c>.</summary>
    [JsonPropertyName("PartyCountryOfResidence")]
    public CountryModel? PartyCountryOfResidence { get; init; }

    /// <summary>BIAN <c>ResidencyStatus</c>: <c>Resident</c> or <c>NonResident</c> in its <c>StatusDescription</c>.</summary>
    [JsonPropertyName("ResidencyStatus")]
    public ResidencyStatusModel? ResidencyStatus { get; init; }

    /// <summary>BIAN <c>ResidentialAddress</c> (1.19): an ISO 20022 postal address.</summary>
    [JsonPropertyName("ResidentialAddress")]
    public PostalAddressModel? ResidentialAddress { get; init; }

    /// <summary>BIAN <c>PartyAddress</c> (1.19): an ISO 20022 postal address.</summary>
    [JsonPropertyName("PartyAddress")]
    public PostalAddressModel? PartyAddress { get; init; }

    /// <summary>BIAN <c>CorporateAddress</c> (1.19): an ISO 20022 postal address.</summary>
    [JsonPropertyName("CorporateAddress")]
    public PostalAddressModel? CorporateAddress { get; init; }

    /// <summary>BIAN <c>eMailAddress</c> (1.19): the address in its <c>EmailAddress</c> (ADR 0031).</summary>
    [JsonPropertyName("eMailAddress")]
    public EmailAddressModel? EMailAddress { get; init; }

    /// <summary>BIAN <c>CellPhoneNumber</c> (1.19): the E.164 number in its <c>MobileNumber</c> (ADR 0031).</summary>
    [JsonPropertyName("CellPhoneNumber")]
    public MobileNumberModel? CellPhoneNumber { get; init; }

    /// <summary>BIAN <c>SocialNetworkContacts</c> (1.19): the https profile URL in its <c>URLAddress</c> (ADR 0031).</summary>
    [JsonPropertyName("SocialNetworkContacts")]
    public SocialNetworkContactModel? SocialNetworkContacts { get; init; }

    /// <summary>
    /// BIAN <c>PartyLegalStructure</c> (1.20), as a plain string (BIAN's enum wrapper flattened, ADR 0024), for example
    /// <c>PrivateLimitedCompany</c> or <c>Co-Operative</c>.
    /// </summary>
    [JsonPropertyName("PartyLegalStructure")]
    public string? PartyLegalStructure { get; init; }

    /// <summary>
    /// BIAN <c>PoliticalExposureType</c> (1.20), as a plain string: <c>Politicalexposureforeign</c>,
    /// <c>Politicalexposuredomestic</c> or <c>Nopoliticalexposure</c>.
    /// </summary>
    [JsonPropertyName("PoliticalExposureType")]
    public string? PoliticalExposureType { get; init; }

    /// <summary>BIAN <c>PoliticalExposureDescriptionRecord</c> (1.20, Max350Text), only with a foreign or domestic exposure.</summary>
    [JsonPropertyName("PoliticalExposureDescriptionRecord")]
    public string? PoliticalExposureDescriptionRecord { get; init; }

    /// <summary>BIAN <c>CustomerSinceDate</c> (1.20): an ISO 8601 date, or a date-time whose date is kept as written.</summary>
    [JsonPropertyName("CustomerSinceDate")]
    public string? CustomerSinceDate { get; init; }

    /// <summary>BIAN <c>GovernmentIssuedDocumentDetails</c> (1.20): the declared identity document.</summary>
    [JsonPropertyName("GovernmentIssuedDocumentDetails")]
    public PartyDocumentModel? GovernmentIssuedDocumentDetails { get; init; }

    /// <summary>BIAN <c>DocumentDirectoryEntryInstanceReference</c> (1.20): the opaque document directory reference.</summary>
    [JsonPropertyName("DocumentDirectoryEntryInstanceReference")]
    public DocumentDirectoryEntryModel? DocumentDirectoryEntryInstanceReference { get; init; }

    /// <summary>
    /// BIAN <c>CorporateCustomerReference</c> (1.21, ADR 0034): the company, as the id of a registered organisation in its
    /// <c>PartyReference</c>.
    /// </summary>
    [JsonPropertyName("CorporateCustomerReference")]
    public InvolvedPartyModel? CorporateCustomerReference { get; init; }

    /// <summary>
    /// BIAN <c>CorporateCustomerLegalEntityReference</c> (1.21): the legal entity, as the id of a registered organisation in
    /// its <c>PartyReference</c>.
    /// </summary>
    [JsonPropertyName("CorporateCustomerLegalEntityReference")]
    public InvolvedPartyModel? CorporateCustomerLegalEntityReference { get; init; }

    /// <summary>
    /// BIAN <c>CompanyOfficerReference</c> (1.21): the company officer, as the id of a registered person in its
    /// <c>PartyReference</c>.
    /// </summary>
    [JsonPropertyName("CompanyOfficerReference")]
    public InvolvedPartyModel? CompanyOfficerReference { get; init; }

    /// <summary>
    /// BIAN <c>CompanyOfficerRole</c> (1.21, Max35Text): the officer's role as a plain string, for example <c>Director</c>.
    /// BIAN types it as a <c>Party</c>, which has no role property (ADR 0034): an object is read for its parts, which are
    /// all refused, and an empty one clears the role.
    /// </summary>
    [JsonPropertyName("CompanyOfficerRole")]
    public JsonElement? CompanyOfficerRole { get; init; }

    /// <summary>Every other property sent: the BIAN Reference fields RimBank does not record yet are found here.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Other { get; init; }

    /// <summary>
    /// The BIAN names of the Reference fields this request sends with a value that RimBank does not record: the staged
    /// top-level fields (<see cref="PartyReferenceFields.Staged"/>, matched without regard to case) and every other part
    /// of the objects it reads. A field sent as <c>null</c> is not sent.
    /// </summary>
    /// <returns>The field names, in the order sent.</returns>
    public IReadOnlyList<string> UnsupportedFields() =>
    [
        .. Present(Other)
            .Select(name => PartyReferenceFields.Staged.Keys.FirstOrDefault(staged => string.Equals(staged, name, StringComparison.OrdinalIgnoreCase)))
            .OfType<string>(),
        .. PartsOf(PartyReferenceFields.GovernmentIssuedIdentityReference, GovernmentIssuedIdentityReference?.Other),
        .. PartsOf(PartyReferenceFields.PartyLegalEntityReference, PartyLegalEntityReference?.Other),
        .. PartsOf(PartyReferenceFields.Nationality, Nationality?.Other),
        .. PartsOf(PartyReferenceFields.PartyCountryOfResidence, PartyCountryOfResidence?.Other),
        .. PartsOf(PartyReferenceFields.ResidencyStatus, ResidencyStatus?.Other),
        .. AddressPartsOf(PartyReferenceFields.ResidentialAddress, ResidentialAddress),
        .. AddressPartsOf(PartyReferenceFields.PartyAddress, PartyAddress),
        .. AddressPartsOf(PartyReferenceFields.CorporateAddress, CorporateAddress),
        .. PartsOf(PartyReferenceFields.EMailAddress, EMailAddress?.Other),
        .. PartsOf(PartyReferenceFields.CellPhoneNumber, CellPhoneNumber?.Other),
        .. PartsOf(PartyReferenceFields.SocialNetworkContacts, SocialNetworkContacts?.Other),
        .. GovernmentIssuedDocumentDetails?.UnsupportedParts(PartyReferenceFields.GovernmentIssuedDocumentDetails) ?? [],
        .. PartsOf(PartyReferenceFields.DocumentDirectoryEntryInstanceReference, DocumentDirectoryEntryInstanceReference?.Other),
        .. PartsOf(PartyReferenceFields.CorporateCustomerReference, CorporateCustomerReference?.Other),
        .. PartsOf(PartyReferenceFields.CorporateCustomerLegalEntityReference, CorporateCustomerLegalEntityReference?.Other),
        .. PartsOf(PartyReferenceFields.CompanyOfficerReference, CompanyOfficerReference?.Other),
        .. RolePartsOf(CompanyOfficerRole),
    ];

    /// <summary>
    /// The command this request asks for, <c>party.reference-field-not-supported</c> for a field RimBank does not record
    /// yet, or <c>party.reference-invalid</c> for an invalid value.
    /// </summary>
    /// <param name="entryId">The entry the route names.</param>
    /// <param name="referenceId">The Reference the route names.</param>
    /// <param name="expectedVersion">The version from <c>If-Match</c> (ADR 0027).</param>
    /// <param name="idempotencyKey">The request's <c>Idempotency-Key</c> (ADR 0018).</param>
    /// <returns>The command, or the validation error.</returns>
    public Result<UpdatePartyReference> ToCommand(
        PartyReferenceDataDirectoryEntryId entryId,
        PartyReferenceId referenceId,
        long expectedVersion,
        IdempotencyKey idempotencyKey) =>
        PartyReferenceChanges
            .Create(
                new PartyReferenceInput(
                    GovernmentIssuedIdentityReference?.TaxIdentificationNumber,
                    GovernmentIssuedIdentityReference?.NationalRegistrationNumber,
                    PartyLegalEntityReference?.Identification,
                    PartyFullLegalName,
                    PartyNameSalutation,
                    DateofBirth,
                    Nationality is { } nationality ? new PartyCountryInput(nationality.Code) : null,
                    PartyCountryOfResidence is { } residence ? new PartyCountryInput(residence.Code) : null,
                    ResidencyStatus is { } status ? new ResidencyStatusInput(status.StatusDescription, status.StatusDateTime) : null,
                    ResidentialAddress?.ToInput(),
                    PartyAddress?.ToInput(),
                    CorporateAddress?.ToInput(),
                    EMailAddress is { } email ? email.EmailAddress ?? string.Empty : null,
                    CellPhoneNumber is { } phone ? phone.MobileNumber ?? string.Empty : null,
                    SocialNetworkContacts is { } social ? social.URLAddress ?? string.Empty : null,
                    PartyLegalStructure,
                    PoliticalExposureType,
                    PoliticalExposureDescriptionRecord,
                    CustomerSinceDate,
                    GovernmentIssuedDocumentDetails?.ToInput(),
                    DocumentDirectoryEntryInstanceReference is { } directory
                        ? directory.DocumentDirectoryEntryIdentification ?? string.Empty
                        : null,
                    PartyReferenceOf(CorporateCustomerReference),
                    PartyReferenceOf(CorporateCustomerLegalEntityReference),
                    PartyReferenceOf(CompanyOfficerReference),
                    RoleOf(CompanyOfficerRole)),
                UnsupportedFields())
            .Map(changes => new UpdatePartyReference(entryId, referenceId, expectedVersion, changes, idempotencyKey));

    private static string? PartyReferenceOf(InvolvedPartyModel? reference) =>
        reference switch
        {
            null => null,
            _ => reference.PartyReference ?? string.Empty,
        };

    // A string is the role; null is not sent; an object (the BIAN Party shape) has no role, so it clears the role and its
    // parts are reported as unsupported; any other JSON value is read as its text, which the role then checks.
    private static string? RoleOf(JsonElement? role) =>
        role switch
        {
            null or { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
            { ValueKind: JsonValueKind.String } text => text.GetString(),
            { ValueKind: JsonValueKind.Object } => string.Empty,
            { } other => other.GetRawText(),
        };

    private static IEnumerable<string> RolePartsOf(JsonElement? role) =>
        role switch
        {
            { ValueKind: JsonValueKind.Object } parts => parts.EnumerateObject()
                .Where(part => part.Value.ValueKind != JsonValueKind.Null)
                .Select(part => $"{PartyReferenceFields.CompanyOfficerRole}.{part.Name}"),
            _ => [],
        };

    private static IEnumerable<string> AddressPartsOf(string field, PostalAddressModel? address) =>
        PartsOf(field, address?.Other).Concat(PartsOf($"{field}.{PostalAddress.CountryPart}", address?.Country?.Other));

    private static IEnumerable<string> PartsOf(string field, Dictionary<string, JsonElement>? parts) =>
        ReferenceModelParts.Present(field, parts);

    private static IEnumerable<string> Present(Dictionary<string, JsonElement>? properties) =>
        ReferenceModelParts.Present(properties);
}
