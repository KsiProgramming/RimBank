using System.Text.Json.Serialization;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The role the company officer plays for the corporate customer: BIAN Reference <c>CompanyOfficerRole</c> (roadmap 1.21,
/// ADR 0034), for example <c>Director</c>. BIAN types it as an ISO 20022 <c>Party</c>, which has no role property, so it is
/// read as the role text BIAN describes: trimmed, not blank and at most 35 characters (ISO 20022 Max35Text). It is only
/// recorded with a company officer.
/// </summary>
[JsonConverter(typeof(PartyReferenceTextJsonConverter<CompanyOfficerRole>))]
public sealed record CompanyOfficerRole : IPartyReferenceText<CompanyOfficerRole>
{
    /// <summary>ISO 20022 Max35Text.</summary>
    public const int MaxLength = 35;

    private CompanyOfficerRole(string value) => Value = value;

    /// <inheritdoc />
    public string Value { get; }

    /// <inheritdoc />
    public static Result<CompanyOfficerRole> TryCreate(string? text) =>
        PartyReferenceText.Read(text, MaxLength, "company officer role").Map(value => new CompanyOfficerRole(value));

    /// <inheritdoc />
    public override string ToString() => Value;
}
