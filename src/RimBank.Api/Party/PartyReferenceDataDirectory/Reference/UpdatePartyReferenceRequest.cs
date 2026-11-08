using System.Text.Json;
using System.Text.Json.Serialization;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// The body of <c>PUT …/{partyreferencedatadirectoryid}/Reference/{referenceid}/Update</c>: BIAN's
/// <c>UpdateReference</c> with its property names (ADR 0024, ADR 0029), as a partial update of the party identifiers. An
/// identifier left out keeps its value, and an empty one clears it. The other BIAN Reference fields are refused until
/// the roadmap phase that records them (1.18–1.20); properties that are not BIAN's are ignored.
/// </summary>
public sealed record UpdatePartyReferenceRequest
{
    /// <summary>BIAN <c>GovernmentIssuedIdentityReference</c>: the tax identification and national registration numbers.</summary>
    [JsonPropertyName("GovernmentIssuedIdentityReference")]
    public GovernmentIssuedIdentityModel? GovernmentIssuedIdentityReference { get; init; }

    /// <summary>BIAN <c>PartyLegalEntityReference</c>: the LEI in its <c>Identification</c>.</summary>
    [JsonPropertyName("PartyLegalEntityReference")]
    public PartyLegalEntityModel? PartyLegalEntityReference { get; init; }

    /// <summary>Every other property sent: the BIAN Reference fields RimBank does not record yet are found here.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Other { get; init; }

    /// <summary>
    /// The BIAN names of the Reference fields this request sends with a value that RimBank does not record: the staged
    /// top-level fields (<see cref="PartyReferenceFields.Staged"/>, matched without regard to case) and every other part
    /// of the two objects it reads. A field sent as <c>null</c> is not sent.
    /// </summary>
    /// <returns>The field names, in the order sent.</returns>
    public IReadOnlyList<string> UnsupportedFields() =>
    [
        .. Present(Other)
            .Select(name => PartyReferenceFields.Staged.Keys.FirstOrDefault(staged => string.Equals(staged, name, StringComparison.OrdinalIgnoreCase)))
            .OfType<string>(),
        .. Present(GovernmentIssuedIdentityReference?.Other).Select(name => $"{PartyReferenceFields.GovernmentIssuedIdentityReference}.{name}"),
        .. Present(PartyLegalEntityReference?.Other).Select(name => $"{PartyReferenceFields.PartyLegalEntityReference}.{name}"),
    ];

    /// <summary>
    /// The command this request asks for, <c>party.reference-field-not-supported</c> for a field RimBank does not record
    /// yet, or <c>party.reference-invalid</c> for an invalid identifier.
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
                GovernmentIssuedIdentityReference?.TaxIdentificationNumber,
                GovernmentIssuedIdentityReference?.NationalRegistrationNumber,
                PartyLegalEntityReference?.Identification,
                UnsupportedFields())
            .Map(changes => new UpdatePartyReference(entryId, referenceId, expectedVersion, changes, idempotencyKey));

    private static IEnumerable<string> Present(Dictionary<string, JsonElement>? properties) =>
        (properties ?? []).Where(property => property.Value.ValueKind != JsonValueKind.Null).Select(property => property.Key);
}
