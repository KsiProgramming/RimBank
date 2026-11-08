using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The party's contact points, held by the entry's Reference Behavior Qualifier (roadmap 1.19, ADR 0031). BIAN gives one
/// <c>ContactPoint</c> per field, so each is a single current value, and an unknown one is <c>null</c>. Personal data (7.2).
/// </summary>
/// <param name="EmailAddress">BIAN <c>eMailAddress</c>.</param>
/// <param name="MobileNumber">BIAN <c>CellPhoneNumber</c>.</param>
/// <param name="SocialNetwork">BIAN <c>SocialNetworkContacts</c>.</param>
public sealed record PartyContactPoints(
    EmailAddress? EmailAddress,
    MobileNumber? MobileNumber,
    SocialNetworkAddress? SocialNetwork)
{
    /// <summary>No contact point recorded.</summary>
    public static PartyContactPoints Empty { get; } = new(null, null, null);

    /// <summary>Whether any contact point is recorded. Computed, so it is not stored.</summary>
    [JsonIgnore]
    public bool HasAny => this != Empty;

    /// <summary>The contact points after a partial update: a contact point the changes leave out keeps its value.</summary>
    /// <param name="changes">The parsed changes.</param>
    /// <returns>The resulting contact points.</returns>
    public PartyContactPoints Apply(PartyReferenceChanges changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        return new PartyContactPoints(
            changes.EmailAddress.ApplyTo(EmailAddress),
            changes.MobileNumber.ApplyTo(MobileNumber),
            changes.SocialNetworkAddress.ApplyTo(SocialNetwork));
    }
}
