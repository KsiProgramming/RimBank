using System.Text.Json;
using System.Text.Json.Serialization;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// The body of <c>PUT …/{partyreferencedatadirectoryid}/Demographics/{demographicsid}/Exchange</c>: BIAN's
/// <c>ExchangeDemographics</c> plus RimBank's extension field <see cref="ExchangeActionType"/> (ADR 0028). Exchange
/// verifies or rejects the details already recorded, so BIAN's detail fields are accepted and ignored; change the
/// details with Update.
/// </summary>
public sealed record ExchangeDemographicsRequest
{
    /// <summary>RimBank's extension: <c>Verify</c> or <c>Reject</c> (case-sensitive), required.</summary>
    [JsonPropertyName("ExchangeActionType")]
    public string? ExchangeActionType { get; init; }

    /// <summary>BIAN <c>Socio-EconomicClassification</c>. Accepted and ignored.</summary>
    [JsonPropertyName("Socio-EconomicClassification")]
    public JsonElement? SocioEconomicClassification { get; init; }

    /// <summary>BIAN <c>EthnicityReligion</c>. Accepted and ignored: it is never stored or returned (ADR 0024).</summary>
    [JsonPropertyName("EthnicityReligion")]
    public JsonElement? EthnicityReligion { get; init; }

    /// <summary>BIAN <c>Employment</c>. Accepted and ignored.</summary>
    [JsonPropertyName("Employment")]
    public JsonElement? Employment { get; init; }

    /// <summary>BIAN <c>EmploymentHistory</c>. Accepted and ignored.</summary>
    [JsonPropertyName("EmploymentHistory")]
    public JsonElement? EmploymentHistory { get; init; }

    /// <summary>BIAN <c>EducationHistory</c>. Accepted and ignored.</summary>
    [JsonPropertyName("EducationHistory")]
    public JsonElement? EducationHistory { get; init; }

    /// <summary>BIAN <c>ServicingConstraints</c>. Accepted and ignored.</summary>
    [JsonPropertyName("ServicingConstraints")]
    public JsonElement? ServicingConstraints { get; init; }

    /// <summary>
    /// The command this request asks for, or <c>party.exchange-action-type-required</c> /
    /// <c>party.exchange-action-type-invalid</c>.
    /// </summary>
    /// <param name="entryId">The entry the route names.</param>
    /// <param name="demographicsId">The Demographics the route names.</param>
    /// <param name="expectedVersion">The version from <c>If-Match</c> (ADR 0027).</param>
    /// <param name="idempotencyKey">The request's <c>Idempotency-Key</c> (ADR 0018).</param>
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
