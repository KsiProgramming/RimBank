using System.Text.Json;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The opaque document directory reference of the Reference BQ (roadmap 1.20, ADR 0032): a Max35Text, never resolved.
public sealed class DocumentDirectoryEntryReferenceTests
{
    [Fact]
    public void It_is_trimmed() =>
        DocumentDirectoryEntryReference.TryCreate(" DOC-000123 ").Value.Value.ShouldBe("DOC-000123");

    [Fact]
    public void Thirty_five_characters_are_accepted() =>
        DocumentDirectoryEntryReference.TryCreate(new string('D', 35)).IsSuccess.ShouldBeTrue();

    [Theory]
    [InlineData(36)]
    [InlineData(0)]
    public void Too_long_or_blank_is_invalid(int length) =>
        DocumentDirectoryEntryReference.TryCreate(new string('D', length)).Error.Code.ShouldBe("party.reference-invalid");

    [Fact]
    public void It_is_a_plain_json_string() =>
        JsonSerializer.Serialize(DocumentDirectoryEntryReference.TryCreate("DOC-1").Value).ShouldBe("\"DOC-1\"");
}
