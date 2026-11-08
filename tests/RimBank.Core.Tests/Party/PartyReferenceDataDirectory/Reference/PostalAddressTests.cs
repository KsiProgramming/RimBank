using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The ISO 20022 postal address of the Reference BQ (roadmap 1.19, ADR 0031): a structured subset of BIAN PostalAddress,
// every part trimmed and at most its BIAN length, with a town and a country required.
public sealed class PostalAddressTests
{
    private static readonly PostalAddressInput _rivoli = new(
        StreetName: " Rue de Rivoli ",
        StreetBuildingIdentification: "12",
        PostCodeIdentification: "75001",
        TownName: " Paris ",
        Country: new PartyCountryInput(" fr "));

    [Fact]
    public void A_full_address_is_read_with_every_part_trimmed() =>
        PostalAddress.TryCreate(new PostalAddressInput(
                " Finance ",
                " Payments ",
                " Rue de Rivoli ",
                " 12 ",
                " Tour A ",
                " 3 ",
                " BP 42 ",
                " 75001 ",
                " Paris ",
                new PartyCountryInput(" fr ")))
            .Value.ShouldBe(new PostalAddress(
                "Finance",
                "Payments",
                "Rue de Rivoli",
                "12",
                "Tour A",
                "3",
                "BP 42",
                "75001",
                "Paris",
                CountryCode.From("FR")));

    [Fact]
    public void Blank_optional_parts_are_not_recorded() =>
        PostalAddress.TryCreate(_rivoli with { StreetBuildingIdentification = "  ", Floor = "" })
            .Value.ShouldSatisfyAllConditions(
                address => address.StreetBuildingIdentification.ShouldBeNull(),
                address => address.Floor.ShouldBeNull(),
                address => address.TownName.ShouldBe("Paris"));

    public static TheoryData<string, int> PartLengths { get; } = new()
    {
        { "Department", 70 },
        { "SubDepartment", 70 },
        { "StreetName", 35 },
        { "StreetBuildingIdentification", 35 },
        { "BuildingName", 35 },
        { "Floor", 16 },
        { "PostOfficeBox", 16 },
        { "PostCodeIdentification", 16 },
        { "TownName", 35 },
    };

    [Theory]
    [MemberData(nameof(PartLengths))]
    public void A_part_at_its_bian_length_is_accepted(string part, int maxLength) =>
        PostalAddress.TryCreate(With(part, new string('a', maxLength))).IsSuccess.ShouldBeTrue();

    [Theory]
    [MemberData(nameof(PartLengths))]
    public void A_part_longer_than_its_bian_length_is_invalid(string part, int maxLength)
    {
        var error = PostalAddress.TryCreate(With(part, new string('a', maxLength + 1))).Error;

        error.Code.ShouldBe("party.reference-invalid");
        error.FieldErrors.ShouldNotBeNull()[part].ShouldBe([$"The {part} must be at most {maxLength} characters."]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void A_missing_town_is_invalid(string? town) =>
        PostalAddress.TryCreate(_rivoli with { TownName = town })
            .Error.FieldErrors.ShouldNotBeNull()["TownName"]
            .ShouldBe(["The TownName is required: a postal address names its town and its country."]);

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void A_missing_country_is_invalid(string? code) =>
        PostalAddress.TryCreate(_rivoli with { Country = code is null ? null : new PartyCountryInput(code) })
            .Error.FieldErrors.ShouldNotBeNull().Keys.ShouldBe(["Country.Code"]);

    [Theory]
    [InlineData("ZZ")]
    [InlineData("FRA")]
    public void An_unknown_country_is_invalid(string code) =>
        PostalAddress.TryCreate(_rivoli with { Country = new PartyCountryInput(code) })
            .Error.FieldErrors.ShouldNotBeNull()["Country.Code"]
            .ShouldBe(["The country must be an ISO 3166-1 alpha-2 code, for example FR."]);

    [Fact]
    public void Every_invalid_part_is_named() =>
        PostalAddress.TryCreate(_rivoli with { StreetName = new string('a', 36), TownName = null, Country = null })
            .Error.FieldErrors.ShouldNotBeNull().Keys.Order().ShouldBe(["Country.Code", "StreetName", "TownName"]);

    [Fact]
    public void An_input_whose_parts_are_all_blank_is_blank()
    {
        new PostalAddressInput().IsBlank.ShouldBeTrue();
        new PostalAddressInput(StreetName: " ", Country: new PartyCountryInput("")).IsBlank.ShouldBeTrue();
        new PostalAddressInput(Floor: "2").IsBlank.ShouldBeFalse();
        new PostalAddressInput(Country: new PartyCountryInput("FR")).IsBlank.ShouldBeFalse();
    }

    private static PostalAddressInput With(string part, string value) =>
        part switch
        {
            "Department" => _rivoli with { Department = value },
            "SubDepartment" => _rivoli with { SubDepartment = value },
            "StreetName" => _rivoli with { StreetName = value },
            "StreetBuildingIdentification" => _rivoli with { StreetBuildingIdentification = value },
            "BuildingName" => _rivoli with { BuildingName = value },
            "Floor" => _rivoli with { Floor = value },
            "PostOfficeBox" => _rivoli with { PostOfficeBox = value },
            "PostCodeIdentification" => _rivoli with { PostCodeIdentification = value },
            "TownName" => _rivoli with { TownName = value },
            _ => throw new ArgumentOutOfRangeException(nameof(part), part, "Not a recorded PostalAddress part."),
        };
}
