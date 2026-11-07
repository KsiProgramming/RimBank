using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// Roadmap 1.7: an entry id the directory never issues is one validation error, defined in the core so that the HTTP
// (1.7) and MCP (1.16) adapters answer it the same way (ADR 0024).
public sealed class EntryIdInvalidTests
{
    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("3f2504e0-4f89-41d3-9a0c-0305e82c3301")]
    public void An_id_the_directory_never_issues_fails_with_the_code_of_the_entry_id_error(string text)
    {
        var parsed = PartyReferenceDataDirectoryEntryId.TryCreate(text);

        parsed.IsFailure.ShouldBeTrue();
        parsed.Error.Code.ShouldBe(PartyReferenceDataDirectoryEntryErrors.EntryIdInvalid.Code);
    }

    [Fact]
    public void The_entry_id_error_is_a_validation_error_that_names_the_BIAN_parameter()
    {
        var error = PartyReferenceDataDirectoryEntryErrors.EntryIdInvalid;

        error.Kind.ShouldBe(ErrorKind.Validation);
        error.Code.ShouldBe("shared.invalid-identifier");
        error.FieldErrors.Keys.ShouldBe(["partyreferencedatadirectoryid"]);
    }
}
