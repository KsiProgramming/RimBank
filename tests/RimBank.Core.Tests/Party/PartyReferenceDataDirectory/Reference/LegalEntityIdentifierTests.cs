using System.Text.Json;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The LEI (ISO 17442) of the Reference BQ (roadmap 1.10, ADR 0029): 18 letters or digits, then 2 check digits that pass
// ISO 7064 MOD 97-10.
public sealed class LegalEntityIdentifierTests
{
    [Theory]
    [InlineData("5493001KJTIIGC8Y1R12")]
    [InlineData("529900T8BM49AURSDO55")]
    public void A_valid_lei_is_accepted(string lei) =>
        LegalEntityIdentifier.TryCreate(lei).Value.Value.ShouldBe(lei);

    [Fact]
    public void An_lei_is_trimmed_and_upper_cased() =>
        LegalEntityIdentifier.TryCreate("  5493001kjtiigc8y1r12 ").Value.Value.ShouldBe("5493001KJTIIGC8Y1R12");

    [Theory]
    [InlineData("5493001KJTIIGC8Y1R1")]
    [InlineData("5493001KJTIIGC8Y1R123")]
    [InlineData("5493001KJTIIGC8Y1R1A")]
    [InlineData("5493001KJTIIGC8Y-R12")]
    [InlineData("5493001KJTIIGC8Y1R 2")]
    public void A_value_that_is_not_shaped_like_an_lei_is_invalid(string lei)
    {
        var error = LegalEntityIdentifier.TryCreate(lei).Error;

        error.Kind.ShouldBe(ErrorKind.Validation);
        error.Code.ShouldBe("party.reference-invalid");
        error.Message.ShouldContain("20 characters");
    }

    [Theory]
    [InlineData("5493001KJTIIGC8Y1R13")]
    [InlineData("5493001KJTIIGC8Y1R21")]
    [InlineData("6493001KJTIIGC8Y1R12")]
    [InlineData("5493001KJTIIGC8Z1R12")]
    public void One_changed_character_fails_the_check_digits(string lei) =>
        LegalEntityIdentifier.TryCreate(lei).Error.Message.ShouldContain("check digits");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_lei_is_invalid(string? lei) =>
        LegalEntityIdentifier.TryCreate(lei).Error.Code.ShouldBe("party.reference-invalid");

    [Fact]
    public void An_lei_is_a_plain_json_string()
    {
        var lei = LegalEntityIdentifier.TryCreate("5493001KJTIIGC8Y1R12").Value;

        var json = JsonSerializer.Serialize(lei);

        json.ShouldBe("\"5493001KJTIIGC8Y1R12\"");
        JsonSerializer.Deserialize<LegalEntityIdentifier>(json).ShouldBe(lei);
    }

    [Theory]
    [InlineData("\"5493001KJTIIGC8Y1R13\"")]
    [InlineData("42")]
    public void Json_that_is_not_a_valid_lei_cannot_be_read(string json) =>
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<LegalEntityIdentifier>(json));
}
