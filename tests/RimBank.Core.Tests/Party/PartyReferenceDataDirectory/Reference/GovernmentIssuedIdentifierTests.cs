using System.Text.Json;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The tax identification number and the national registration number of the Reference BQ (roadmap 1.10, ADR 0029):
// trimmed ISO 20022 Max35Text, with no country format check.
public sealed class GovernmentIssuedIdentifierTests
{
    [Fact]
    public void A_tax_identification_number_is_trimmed() =>
        TaxIdentificationNumber.TryCreate("  FR 123 ").Value.Value.ShouldBe("FR 123");

    [Fact]
    public void A_national_registration_number_is_trimmed() =>
        NationalRegistrationNumber.TryCreate(" 1 85 05 ").Value.Value.ShouldBe("1 85 05");

    [Fact]
    public void Thirty_five_characters_are_accepted()
    {
        var text = new string('7', 35);

        TaxIdentificationNumber.TryCreate(text).IsSuccess.ShouldBeTrue();
        NationalRegistrationNumber.TryCreate(text).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Thirty_six_characters_are_invalid()
    {
        var text = new string('7', 36);

        TaxIdentificationNumber.TryCreate(text).Error.ShouldSatisfyAllConditions(
            error => error.Kind.ShouldBe(ErrorKind.Validation),
            error => error.Code.ShouldBe("party.reference-invalid"),
            error => error.Message.ShouldBe("The tax identification number must be at most 35 characters."));
        NationalRegistrationNumber.TryCreate(text).Error.Message
            .ShouldBe("The national registration number must be at most 35 characters.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void A_blank_number_is_invalid(string? text)
    {
        TaxIdentificationNumber.TryCreate(text).IsFailure.ShouldBeTrue();
        NationalRegistrationNumber.TryCreate(text).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void The_numbers_are_plain_json_strings()
    {
        var tax = TaxIdentificationNumber.TryCreate("FR12345678901").Value;
        var national = NationalRegistrationNumber.TryCreate("185057800608436").Value;

        JsonSerializer.Serialize(tax).ShouldBe("\"FR12345678901\"");
        JsonSerializer.Deserialize<TaxIdentificationNumber>("\"FR12345678901\"").ShouldBe(tax);
        JsonSerializer.Deserialize<NationalRegistrationNumber>(JsonSerializer.Serialize(national)).ShouldBe(national);
    }
}
