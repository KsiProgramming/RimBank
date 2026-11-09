using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// The result of the Demographics tools: the fields of the HTTP Demographics response with BIAN's names, which it
/// mirrors (a parity test proves the JSON is equal, ADR 0039), and the entry's <see cref="Version"/>, which HTTP sends as
/// the <c>ETag</c> header. Commands build it from their acknowledgement, Retrieve from the entry's view.
/// </summary>
/// <param name="PartyReferenceDataDirectoryEntryId">The entry.</param>
/// <param name="DemographicsId">The entry's Demographics.</param>
/// <param name="SocioEconomicClassification">BIAN <c>Socio-EconomicClassification</c>, when known.</param>
/// <param name="Employment">BIAN <c>Employment</c>, when known.</param>
/// <param name="EmploymentHistory">BIAN <c>EmploymentHistory</c>, when known.</param>
/// <param name="EducationHistory">BIAN <c>EducationHistory</c>, when known.</param>
/// <param name="ServicingConstraints">BIAN <c>ServicingConstraints</c>, when known.</param>
/// <param name="VerificationStatus">Whether the bank verified the details (ADR 0028).</param>
/// <param name="VerificationDate">When the bank verified or rejected them.</param>
/// <param name="Version">The entry's version: the <c>expectedVersion</c> of a later command (ADR 0027).</param>
public sealed record DemographicsResult(
    [property: JsonPropertyName("PartyReferenceDataDirectoryEntryId")] Guid PartyReferenceDataDirectoryEntryId,
    [property: JsonPropertyName("DemographicsId")] Guid DemographicsId,
    [property: JsonPropertyName("Socio-EconomicClassification"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SocioEconomicClassification,
    [property: JsonPropertyName("Employment"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DemographicsEmployment? Employment,
    [property: JsonPropertyName("EmploymentHistory"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? EmploymentHistory,
    [property: JsonPropertyName("EducationHistory"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? EducationHistory,
    [property: JsonPropertyName("ServicingConstraints"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ServicingConstraints,
    [property: JsonPropertyName("VerificationStatus")] string VerificationStatus,
    [property: JsonPropertyName("VerificationDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? VerificationDate,
    [property: JsonPropertyName("version")] long Version)
{
    /// <summary>The result of an Update.</summary>
    /// <param name="acknowledged">The outcome of <see cref="UpdateDemographics"/>.</param>
    /// <returns>The tool's result.</returns>
    public static DemographicsResult From(UpdateDemographicsAcknowledged acknowledged)
    {
        ArgumentNullException.ThrowIfNull(acknowledged);
        return From(acknowledged.EntryId, acknowledged.DemographicsId, acknowledged.Demographics, acknowledged.Version);
    }

    /// <summary>The result of an Exchange.</summary>
    /// <param name="acknowledged">The outcome of <see cref="ExchangeDemographics"/>.</param>
    /// <returns>The tool's result.</returns>
    public static DemographicsResult From(ExchangeDemographicsAcknowledged acknowledged)
    {
        ArgumentNullException.ThrowIfNull(acknowledged);
        return From(acknowledged.EntryId, acknowledged.DemographicsId, acknowledged.Demographics, acknowledged.Version);
    }

    /// <summary>The result of a Retrieve.</summary>
    /// <param name="view">The entry's read model.</param>
    /// <returns>The tool's result.</returns>
    public static DemographicsResult From(PartyReferenceDataDirectoryEntryView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return From(view.EntryId, view.DemographicsId, view.CurrentDemographics, view.Version);
    }

    private static DemographicsResult From(
        PartyReferenceDataDirectoryEntryId entryId,
        DemographicsId demographicsId,
        DemographicsRecord demographics,
        long version) =>
        new(
            entryId.Value,
            demographicsId.Value,
            demographics.Details.SocioEconomicClassification,
            DemographicsEmployment.From(demographics.Details.Employment),
            demographics.Details.EmploymentHistory,
            demographics.Details.EducationHistory,
            demographics.Details.ServicingConstraints,
            demographics.VerificationStatus.ToString(),
            demographics.VerificationDate,
            version);
}
