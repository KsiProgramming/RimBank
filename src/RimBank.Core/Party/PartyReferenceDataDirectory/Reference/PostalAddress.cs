using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// A postal address held by the entry's Reference Behavior Qualifier: BIAN <c>ResidentialAddress</c>, <c>PartyAddress</c>
/// and <c>CorporateAddress</c>, each an ISO 20022 <c>PostalAddress</c> (roadmap 1.19, ADR 0031). RimBank records the
/// structured parts ISO 20022 payment messages use, each trimmed and at most its BIAN length, and requires a town and a
/// country (the CBPR+ structured-address rule). An unknown optional part is <c>null</c>. Personal data (7.2).
/// </summary>
/// <param name="Department">BIAN <c>Department</c> (Max70Text).</param>
/// <param name="SubDepartment">BIAN <c>SubDepartment</c> (Max70Text).</param>
/// <param name="StreetName">BIAN <c>StreetName</c> (Max35Text).</param>
/// <param name="StreetBuildingIdentification">BIAN <c>StreetBuildingIdentification</c>: the building number (Max35Text).</param>
/// <param name="BuildingName">BIAN <c>BuildingName</c> (Max35Text).</param>
/// <param name="Floor">BIAN <c>Floor</c> (Max16Text).</param>
/// <param name="PostOfficeBox">BIAN <c>PostOfficeBox</c> (Max16Text).</param>
/// <param name="PostCodeIdentification">BIAN <c>PostCodeIdentification</c>: the post code (Max16Text).</param>
/// <param name="TownName">BIAN <c>TownName</c> (Max35Text), required.</param>
/// <param name="Country">BIAN <c>Country.Code</c> (ISO 3166-1 alpha-2, ADR 0030), required.</param>
public sealed record PostalAddress(
    string? Department,
    string? SubDepartment,
    string? StreetName,
    string? StreetBuildingIdentification,
    string? BuildingName,
    string? Floor,
    string? PostOfficeBox,
    string? PostCodeIdentification,
    string TownName,
    CountryCode Country)
{
    /// <summary>BIAN <c>TownName</c>.</summary>
    public const string TownNamePart = "TownName";

    /// <summary>BIAN <c>Country</c>, a <c>Country</c> object.</summary>
    public const string CountryPart = "Country";

    /// <summary>BIAN <c>Country.Code</c>: the field-error key of an invalid or missing country.</summary>
    public const string CountryCodePart = CountryPart + "." + PartyReferenceFields.CountryCodePart;

    /// <summary>
    /// The BIAN names of the parts RimBank records, with their ISO 20022 maximum lengths. <c>Country</c> is read apart.
    /// </summary>
    public static IReadOnlyList<(string Part, int MaxLength)> TextParts { get; } =
    [
        ("Department", 70),
        ("SubDepartment", 70),
        ("StreetName", 35),
        ("StreetBuildingIdentification", 35),
        ("BuildingName", 35),
        ("Floor", 16),
        ("PostOfficeBox", 16),
        ("PostCodeIdentification", 16),
        (TownNamePart, 35),
    ];

    /// <summary>
    /// Reads an address a caller sent whose parts are not all blank (<see cref="PostalAddressInput.IsBlank"/>): trims every
    /// part, drops the blank ones, and checks the lengths, the town and the country.
    /// </summary>
    /// <param name="input">The parts sent.</param>
    /// <returns>
    /// The address, or <c>party.reference-invalid</c> naming every invalid part by its BIAN name relative to the address
    /// (for example <c>TownName</c> or <c>Country.Code</c>).
    /// </returns>
    public static Result<PostalAddress> TryCreate(PostalAddressInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var fieldErrors = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        string?[] texts =
        [
            input.Department,
            input.SubDepartment,
            input.StreetName,
            input.StreetBuildingIdentification,
            input.BuildingName,
            input.Floor,
            input.PostOfficeBox,
            input.PostCodeIdentification,
            input.TownName,
        ];
        var parts = TextParts.Select((part, index) => Part(texts[index], part.Part, part.MaxLength, fieldErrors)).ToArray();
        var town = parts[^1];
        if (town is null && !fieldErrors.ContainsKey(TownNamePart))
        {
            fieldErrors[TownNamePart] = ["The TownName is required: a postal address names its town and its country."];
        }

        var country = string.IsNullOrWhiteSpace(input.Country?.Code)
            ? PartyReferenceErrors.ValueInvalid(PartyReferenceCountry.Message)
            : PartyReferenceCountry.Read(input.Country.Code);
        if (country.IsFailure)
        {
            fieldErrors[CountryCodePart] = [country.Error.Message];
        }

        return fieldErrors.Count switch
        {
            0 => new PostalAddress(parts[0], parts[1], parts[2], parts[3], parts[4], parts[5], parts[6], parts[7], town!, country.Value),
            _ => PartyReferenceErrors.ReferenceInvalid(fieldErrors),
        };
    }

    private static string? Part(string? text, string part, int maxLength, Dictionary<string, IReadOnlyList<string>> fieldErrors)
    {
        var trimmed = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        if (trimmed?.Length > maxLength)
        {
            fieldErrors[part] = [$"The {part} must be at most {maxLength} characters."];
            return null;
        }

        return trimmed;
    }
}
