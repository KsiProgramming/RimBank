using System.Text.Json.Serialization;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The party's own description of its political exposure: BIAN Reference <c>PoliticalExposureDescriptionRecord</c>
/// (roadmap 1.20, ADR 0032). It is trimmed, not blank and at most 350 characters (ISO 20022 Max350Text), and is only
/// recorded with a foreign or domestic exposure. Sensitive personal data (7.2).
/// </summary>
[JsonConverter(typeof(PartyReferenceTextJsonConverter<PoliticalExposureDescription>))]
public sealed record PoliticalExposureDescription : IPartyReferenceText<PoliticalExposureDescription>
{
    /// <summary>ISO 20022 Max350Text.</summary>
    public const int MaxLength = 350;

    private PoliticalExposureDescription(string value) => Value = value;

    /// <inheritdoc />
    public string Value { get; }

    /// <inheritdoc />
    public static Result<PoliticalExposureDescription> TryCreate(string? text) =>
        PartyReferenceText.Read(text, MaxLength, "political exposure description").Map(value => new PoliticalExposureDescription(value));

    /// <inheritdoc />
    public override string ToString() => Value;
}
