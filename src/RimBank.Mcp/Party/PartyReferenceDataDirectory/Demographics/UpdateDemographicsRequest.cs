using System.Text.Json;
using System.Text.Json.Serialization;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// The <c>request</c> argument of <c>party_reference_data_directory_update_demographics</c>: BIAN's
/// <c>UpdateDemographics</c> body with its property names, mirroring the HTTP request (ADR 0039, ADR 0040). It is a
/// partial update: a value left out keeps its value, an empty one clears it, and <c>Employment</c> replaces the whole
/// employment. <c>EthnicityReligion</c> is accepted and never stored.
/// </summary>
public sealed record UpdateDemographicsRequest
{
    /// <summary>BIAN <c>Socio-EconomicClassification</c>.</summary>
    [JsonPropertyName("Socio-EconomicClassification")]
    public string? SocioEconomicClassification { get; init; }

    /// <summary>BIAN <c>EthnicityReligion</c>: accepted and never stored.</summary>
    [JsonPropertyName("EthnicityReligion")]
    public JsonElement? EthnicityReligion { get; init; }

    /// <summary>BIAN <c>Employment</c>.</summary>
    [JsonPropertyName("Employment")]
    public DemographicsEmployment? Employment { get; init; }

    /// <summary>BIAN <c>EmploymentHistory</c>.</summary>
    [JsonPropertyName("EmploymentHistory")]
    public string? EmploymentHistory { get; init; }

    /// <summary>BIAN <c>EducationHistory</c>.</summary>
    [JsonPropertyName("EducationHistory")]
    public string? EducationHistory { get; init; }

    /// <summary>BIAN <c>ServicingConstraints</c>.</summary>
    [JsonPropertyName("ServicingConstraints")]
    public string? ServicingConstraints { get; init; }

    /// <summary>The command this request asks for, or the first validation error.</summary>
    /// <param name="entryId">The entry.</param>
    /// <param name="demographicsId">The entry's Demographics.</param>
    /// <param name="expectedVersion">The entry's version the caller last read (ADR 0027).</param>
    /// <param name="idempotencyKey">The command's idempotency key (ADR 0018).</param>
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
