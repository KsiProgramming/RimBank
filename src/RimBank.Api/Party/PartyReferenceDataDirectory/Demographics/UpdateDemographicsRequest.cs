using System.Text.Json;
using System.Text.Json.Serialization;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// The body of <c>PUT …/{partyreferencedatadirectoryid}/Demographics/{demographicsid}/Update</c>: BIAN's
/// <c>UpdateDemographics</c> with its property names (ADR 0024), as a partial update. A field left out keeps its value,
/// an empty one clears it, and a given <see cref="Employment"/> replaces the whole employment.
/// <see cref="EthnicityReligion"/> is accepted and ignored: RimBank keeps no special-category personal data.
/// </summary>
public sealed record UpdateDemographicsRequest
{
    /// <summary>BIAN <c>Socio-EconomicClassification</c> (Max140Text).</summary>
    [JsonPropertyName("Socio-EconomicClassification")]
    public string? SocioEconomicClassification { get; init; }

    /// <summary>BIAN <c>EthnicityReligion</c>. Accepted and ignored: it is never stored or returned (ADR 0024).</summary>
    [JsonPropertyName("EthnicityReligion")]
    public JsonElement? EthnicityReligion { get; init; }

    /// <summary>BIAN <c>Employment</c>: replaces the whole employment; an empty one clears it.</summary>
    [JsonPropertyName("Employment")]
    public DemographicsEmployment? Employment { get; init; }

    /// <summary>BIAN <c>EmploymentHistory</c> (Max2048Text).</summary>
    [JsonPropertyName("EmploymentHistory")]
    public string? EmploymentHistory { get; init; }

    /// <summary>BIAN <c>EducationHistory</c> (Max2048Text).</summary>
    [JsonPropertyName("EducationHistory")]
    public string? EducationHistory { get; init; }

    /// <summary>BIAN <c>ServicingConstraints</c> (Max2048Text).</summary>
    [JsonPropertyName("ServicingConstraints")]
    public string? ServicingConstraints { get; init; }

    /// <summary>The command this request asks for, or <c>party.demographics-invalid</c> for a text that is too long.</summary>
    /// <param name="entryId">The entry the route names.</param>
    /// <param name="demographicsId">The Demographics the route names.</param>
    /// <param name="expectedVersion">The version from <c>If-Match</c> (ADR 0027).</param>
    /// <param name="idempotencyKey">The request's <c>Idempotency-Key</c> (ADR 0018).</param>
    /// <returns>The command, or the validation error.</returns>
    public Result<UpdateDemographics> ToCommand(
        PartyReferenceDataDirectoryEntryId entryId,
        DemographicsId demographicsId,
        long expectedVersion,
        IdempotencyKey idempotencyKey) =>
        DemographicsChanges
            .Create(SocioEconomicClassification, Employment?.ToEmployment(), EmploymentHistory, EducationHistory, ServicingConstraints)
            .Map(changes => new UpdateDemographics(entryId, demographicsId, expectedVersion, changes, idempotencyKey));
}
