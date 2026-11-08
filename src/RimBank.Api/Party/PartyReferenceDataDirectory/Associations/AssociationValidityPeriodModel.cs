using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// BIAN's <c>DateTimePeriod</c> as the Associations <c>AssociationValidFromToDate</c>, in requests and responses (roadmap
/// 1.11, ADR 0033). Unlike the document period of a Reference, its bounds are plain ISO 8601 strings. RimBank records
/// <c>FromDateTime</c> and <c>ToDateTime</c>; a request that sends any other part of it is refused.
/// </summary>
public sealed record AssociationValidityPeriodModel
{
    /// <summary>BIAN <c>FromDateTime</c>: when the association starts, an ISO 8601 date-time or date.</summary>
    [JsonPropertyName("FromDateTime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FromDateTime { get; init; }

    /// <summary>BIAN <c>ToDateTime</c>: when the association ends, an ISO 8601 date-time or date.</summary>
    [JsonPropertyName("ToDateTime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToDateTime { get; init; }

    /// <summary>The other parts a request sent (for example <c>NumberOfDays</c>), which RimBank does not record.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Other { get; init; }

    /// <summary>The period as a response shows it: each known bound in UTC, for example <c>2024-01-01T00:00:00Z</c>.</summary>
    /// <param name="period">The recorded period, if any.</param>
    /// <returns>The model, or <c>null</c> when no period is recorded.</returns>
    public static AssociationValidityPeriodModel? From(AssociationValidityPeriod? period) =>
        period switch
        {
            null => null,
            _ => new AssociationValidityPeriodModel { FromDateTime = Format(period.From), ToDateTime = Format(period.To) },
        };

    /// <summary>The raw bounds, for the core to read.</summary>
    /// <returns>The input.</returns>
    public AssociationValidityPeriodInput ToInput() => new(FromDateTime, ToDateTime);

    private static string? Format(DateTimeOffset? bound) =>
        bound?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture);
}
