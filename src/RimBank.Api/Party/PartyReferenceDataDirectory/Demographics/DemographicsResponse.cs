using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// The body of every successful Demographics operation: BIAN's Demographics fields without <c>EthnicityReligion</c>
/// (ADR 0024), the verification RimBank adds (ADR 0028), and the ids of the entry and its Demographics. Update and
/// Exchange build it from their acknowledgement, Retrieve from the entry's view (tech-stack §5). Unknown details are
/// left out, and the entry's version is sent as the <c>ETag</c> header, not in the body.
/// </summary>
/// <param name="PartyReferenceDataDirectoryEntryId">The entry: the <c>{partyreferencedatadirectoryid}</c> of its operations.</param>
/// <param name="DemographicsId">The entry's Demographics: the <c>{demographicsid}</c> of its operations.</param>
/// <param name="SocioEconomicClassification">BIAN <c>Socio-EconomicClassification</c>.</param>
/// <param name="Employment">BIAN <c>Employment</c>.</param>
/// <param name="EmploymentHistory">BIAN <c>EmploymentHistory</c>.</param>
/// <param name="EducationHistory">BIAN <c>EducationHistory</c>.</param>
/// <param name="ServicingConstraints">BIAN <c>ServicingConstraints</c>.</param>
/// <param name="VerificationStatus"><c>Unverified</c>, <c>Verified</c> or <c>Rejected</c> (ADR 0028).</param>
/// <param name="VerificationDate">When the current details were verified or rejected; left out while unverified.</param>
public sealed record DemographicsResponse(
    [property: JsonPropertyName("PartyReferenceDataDirectoryEntryId")] Guid PartyReferenceDataDirectoryEntryId,
    [property: JsonPropertyName("DemographicsId")] Guid DemographicsId,
    [property: JsonPropertyName("Socio-EconomicClassification"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SocioEconomicClassification,
    [property: JsonPropertyName("Employment"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DemographicsEmployment? Employment,
    [property: JsonPropertyName("EmploymentHistory"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? EmploymentHistory,
    [property: JsonPropertyName("EducationHistory"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? EducationHistory,
    [property: JsonPropertyName("ServicingConstraints"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ServicingConstraints,
    [property: JsonPropertyName("VerificationStatus")] string VerificationStatus,
    [property: JsonPropertyName("VerificationDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? VerificationDate)
{
    /// <summary>The response for an Update's acknowledgement.</summary>
    /// <param name="acknowledged">The outcome of <see cref="UpdateDemographics"/>.</param>
    /// <returns>The response body.</returns>
    public static DemographicsResponse From(UpdateDemographicsAcknowledged acknowledged)
    {
        ArgumentNullException.ThrowIfNull(acknowledged);
        return From(acknowledged.EntryId, acknowledged.DemographicsId, acknowledged.Demographics);
    }

    /// <summary>The response for an Exchange's acknowledgement.</summary>
    /// <param name="acknowledged">The outcome of <see cref="ExchangeDemographics"/>.</param>
    /// <returns>The response body.</returns>
    public static DemographicsResponse From(ExchangeDemographicsAcknowledged acknowledged)
    {
        ArgumentNullException.ThrowIfNull(acknowledged);
        return From(acknowledged.EntryId, acknowledged.DemographicsId, acknowledged.Demographics);
    }

    /// <summary>The response for the Demographics section of an entry's view.</summary>
    /// <param name="view">The entry's view, as <see cref="RetrieveDemographics"/> returns it.</param>
    /// <returns>The response body.</returns>
    public static DemographicsResponse From(PartyReferenceDataDirectoryEntryView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return From(view.EntryId, view.DemographicsId, view.CurrentDemographics);
    }

    private static DemographicsResponse From(
        PartyReferenceDataDirectoryEntryId entryId,
        DemographicsId demographicsId,
        DemographicsRecord demographics) =>
        new(
            entryId.Value,
            demographicsId.Value,
            demographics.Details.SocioEconomicClassification,
            DemographicsEmployment.From(demographics.Details.Employment),
            demographics.Details.EmploymentHistory,
            demographics.Details.EducationHistory,
            demographics.Details.ServicingConstraints,
            demographics.VerificationStatus.ToString(),
            demographics.VerificationDate);
}
