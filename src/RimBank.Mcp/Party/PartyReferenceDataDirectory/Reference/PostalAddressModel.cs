using System.Text.Json;
using System.Text.Json.Serialization;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// BIAN's Reference <c>PostalAddress</c> object (<c>ResidentialAddress</c>, <c>PartyAddress</c>, <c>CorporateAddress</c>),
/// in requests and responses. RimBank records its structured parts and the ISO 3166 <c>Country.Code</c> (roadmap 1.19,
/// ADR 0031); a request that sends any other part of it (<c>AddressType</c>, <c>Location</c>, …) is refused.
/// </summary>
public sealed record PostalAddressModel
{
    /// <summary>BIAN <c>Department</c> (Max70Text).</summary>
    [JsonPropertyName("Department")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Department { get; init; }

    /// <summary>BIAN <c>SubDepartment</c> (Max70Text).</summary>
    [JsonPropertyName("SubDepartment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SubDepartment { get; init; }

    /// <summary>BIAN <c>StreetName</c> (Max35Text).</summary>
    [JsonPropertyName("StreetName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StreetName { get; init; }

    /// <summary>BIAN <c>StreetBuildingIdentification</c>: the building number (Max35Text).</summary>
    [JsonPropertyName("StreetBuildingIdentification")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StreetBuildingIdentification { get; init; }

    /// <summary>BIAN <c>BuildingName</c> (Max35Text).</summary>
    [JsonPropertyName("BuildingName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BuildingName { get; init; }

    /// <summary>BIAN <c>Floor</c> (Max16Text).</summary>
    [JsonPropertyName("Floor")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Floor { get; init; }

    /// <summary>BIAN <c>PostOfficeBox</c> (Max16Text).</summary>
    [JsonPropertyName("PostOfficeBox")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PostOfficeBox { get; init; }

    /// <summary>BIAN <c>PostCodeIdentification</c>: the post code (Max16Text).</summary>
    [JsonPropertyName("PostCodeIdentification")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PostCodeIdentification { get; init; }

    /// <summary>BIAN <c>TownName</c> (Max35Text), required in an address.</summary>
    [JsonPropertyName("TownName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TownName { get; init; }

    /// <summary>BIAN <c>Country</c>: the ISO 3166 code in its <c>Code</c>, required in an address.</summary>
    [JsonPropertyName("Country")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CountryModel? Country { get; init; }

    /// <summary>The other parts a request sent (for example <c>AddressType</c>), which RimBank does not record.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Other { get; init; }

    /// <summary>The model of a recorded address.</summary>
    /// <param name="address">The address, or <c>null</c>.</param>
    /// <returns>The model, or <c>null</c> when no address is recorded.</returns>
    public static PostalAddressModel? From(PostalAddress? address) =>
        address switch
        {
            null => null,
            _ => new PostalAddressModel
            {
                Department = address.Department,
                SubDepartment = address.SubDepartment,
                StreetName = address.StreetName,
                StreetBuildingIdentification = address.StreetBuildingIdentification,
                BuildingName = address.BuildingName,
                Floor = address.Floor,
                PostOfficeBox = address.PostOfficeBox,
                PostCodeIdentification = address.PostCodeIdentification,
                TownName = address.TownName,
                Country = new CountryModel { Code = address.Country.Code },
            },
        };

    /// <summary>The parts of this address as the core reads them.</summary>
    /// <returns>The input.</returns>
    public PostalAddressInput ToInput() =>
        new(
            Department,
            SubDepartment,
            StreetName,
            StreetBuildingIdentification,
            BuildingName,
            Floor,
            PostOfficeBox,
            PostCodeIdentification,
            TownName,
            Country is { } country ? new PartyCountryInput(country.Code) : null);
}
