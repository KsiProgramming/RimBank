using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>
/// BIAN's BankRelations <c>BankRelationReference</c> (a <c>PartyRelationship</c>), in requests and responses (roadmap 1.12,
/// ADR 0035). RimBank records the <c>DateTimeContent</c> of <c>RelationshipStartDate</c> and <c>RelationshipEndDate</c>, and
/// the <c>StatusDescription</c> of <c>RelationshipLifecycleStatus</c>; a request that sends any other part (such as
/// <c>RelationshipIdentification</c>) is refused.
/// </summary>
public sealed record BankRelationReferenceModel
{
    /// <summary>BIAN <c>RelationshipStartDate</c>: when the relationship started.</summary>
    [JsonPropertyName("RelationshipStartDate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeModel? RelationshipStartDate { get; init; }

    /// <summary>BIAN <c>RelationshipEndDate</c>: when the relationship ended or ends.</summary>
    [JsonPropertyName("RelationshipEndDate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeModel? RelationshipEndDate { get; init; }

    /// <summary>BIAN <c>RelationshipLifecycleStatus</c>: where the relationship stands.</summary>
    [JsonPropertyName("RelationshipLifecycleStatus")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RelationshipLifecycleStatusModel? RelationshipLifecycleStatus { get; init; }

    /// <summary>The other parts a request sent, which RimBank does not record.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Other { get; init; }

    /// <summary>The recorded parts as raw text: an object sent without its recorded part is sent blank, which clears it.</summary>
    /// <returns>The input the core reads.</returns>
    public BankRelationReferenceInput ToInput() =>
        new(
            RelationshipStartDate is { } start ? start.DateTimeContent ?? string.Empty : null,
            RelationshipEndDate is { } end ? end.DateTimeContent ?? string.Empty : null,
            RelationshipLifecycleStatus is { } status ? status.StatusDescription ?? string.Empty : null);

    /// <summary>The BIAN paths of the parts sent with a value that RimBank does not record.</summary>
    /// <returns>The paths, in the order sent.</returns>
    public IEnumerable<string> UnsupportedParts() =>
    [
        .. ReferenceModelParts.Present(BankRelationsFields.BankRelationReference, Other),
        .. ReferenceModelParts.Present(BankRelationsFields.RelationshipStartDate, RelationshipStartDate?.Other),
        .. ReferenceModelParts.Present(BankRelationsFields.RelationshipEndDate, RelationshipEndDate?.Other),
        .. ReferenceModelParts.Present(BankRelationsFields.RelationshipLifecycleStatus, RelationshipLifecycleStatus?.Other),
    ];

    /// <summary>The recorded relationship parts of some BankRelations, or <c>null</c> when none is known.</summary>
    /// <param name="details">The BankRelations.</param>
    /// <returns>The model, with each unknown part left out.</returns>
    public static BankRelationReferenceModel? From(BankRelationsDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        return details is { StartDate: null, EndDate: null, LifecycleStatus: null }
            ? null
            : new BankRelationReferenceModel
            {
                RelationshipStartDate = DateOf(details.StartDate),
                RelationshipEndDate = DateOf(details.EndDate),
                RelationshipLifecycleStatus = details.LifecycleStatus is { } status
                    ? new RelationshipLifecycleStatusModel { StatusDescription = status.ToBianName() }
                    : null,
            };
    }

    private static DateTimeModel? DateOf(DateTimeOffset? value) =>
        value is { } instant
            ? new DateTimeModel { DateTimeContent = instant.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture) }
            : null;
}

/// <summary>
/// BIAN's <c>RelationshipLifecycleStatus</c> (ISO 20022 <c>Status</c>) inside <see cref="BankRelationReferenceModel"/>
/// (roadmap 1.12). RimBank records <c>StatusDescription</c>; a request that sends any other part of it is refused.
/// </summary>
public sealed record RelationshipLifecycleStatusModel
{
    /// <summary>BIAN <c>StatusDescription</c>: <c>Active</c>, <c>Suspended</c> or <c>Ended</c>.</summary>
    [JsonPropertyName("StatusDescription")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StatusDescription { get; init; }

    /// <summary>The other parts a request sent (for example <c>StatusReason</c>), which RimBank does not record.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Other { get; init; }
}
