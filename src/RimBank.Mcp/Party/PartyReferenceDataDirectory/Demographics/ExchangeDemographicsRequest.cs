using System.Text.Json;
using System.Text.Json.Serialization;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// The <c>request</c> argument of <c>party_reference_data_directory_exchange_demographics</c>: BIAN's
/// <c>ExchangeDemographics</c> body, mirroring the HTTP request (ADR 0039, ADR 0040). <c>ExchangeActionType</c> is
/// RimBank's extension field (ADR 0028); the BIAN detail fields are accepted and ignored.
/// </summary>
public sealed record ExchangeDemographicsRequest
{
    /// <summary>RimBank's <c>ExchangeActionType</c>: <c>Verify</c> or <c>Reject</c>.</summary>
    [JsonPropertyName("ExchangeActionType")]
    public string? ExchangeActionType { get; init; }

    /// <summary>BIAN <c>Socio-EconomicClassification</c>: accepted and ignored.</summary>
    [JsonPropertyName("Socio-EconomicClassification")]
    public JsonElement? SocioEconomicClassification { get; init; }

    /// <summary>BIAN <c>EthnicityReligion</c>: accepted and ignored.</summary>
    [JsonPropertyName("EthnicityReligion")]
    public JsonElement? EthnicityReligion { get; init; }

    /// <summary>BIAN <c>Employment</c>: accepted and ignored.</summary>
    [JsonPropertyName("Employment")]
    public JsonElement? Employment { get; init; }

    /// <summary>BIAN <c>EmploymentHistory</c>: accepted and ignored.</summary>
    [JsonPropertyName("EmploymentHistory")]
    public JsonElement? EmploymentHistory { get; init; }

    /// <summary>BIAN <c>EducationHistory</c>: accepted and ignored.</summary>
    [JsonPropertyName("EducationHistory")]
    public JsonElement? EducationHistory { get; init; }

    /// <summary>BIAN <c>ServicingConstraints</c>: accepted and ignored.</summary>
    [JsonPropertyName("ServicingConstraints")]
    public JsonElement? ServicingConstraints { get; init; }

    /// <summary>The command this request asks for, or the error of a missing or unknown action.</summary>
    /// <param name="entryId">The entry.</param>
    /// <param name="demographicsId">The entry's Demographics.</param>
    /// <param name="expectedVersion">The entry's version the caller last read (ADR 0027).</param>
    /// <param name="idempotencyKey">The command's idempotency key (ADR 0018).</param>
    /// <returns>The command, or the validation error.</returns>
    public Result<ExchangeDemographics> ToCommand(
        PartyReferenceDataDirectoryEntryId entryId,
        DemographicsId demographicsId,
        long expectedVersion,
        IdempotencyKey idempotencyKey) =>
        ExchangeActionTypes
            .FromName(ExchangeActionType)
            .Map(action => new ExchangeDemographics(entryId, demographicsId, expectedVersion, action, idempotencyKey));
}
