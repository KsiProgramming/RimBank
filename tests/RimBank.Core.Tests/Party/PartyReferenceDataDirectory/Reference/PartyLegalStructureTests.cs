namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The declared legal structure of the Reference BQ (roadmap 1.20, ADR 0032): the BIAN code list, read without regard to
// case, spaces or hyphens, and answered in BIAN spelling.
public sealed class PartyLegalStructureTests
{
    [Theory]
    [InlineData("Individual", PartyLegalStructure.Individual)]
    [InlineData("CommunityInterestCompany", PartyLegalStructure.CommunityInterestCompany)]
    [InlineData("CharitableIncorporatedOrganisation", PartyLegalStructure.CharitableIncorporatedOrganisation)]
    [InlineData("Co-Operative", PartyLegalStructure.CoOperative)]
    [InlineData("Charity", PartyLegalStructure.Charity)]
    [InlineData("GeneralPartnership", PartyLegalStructure.GeneralPartnership)]
    [InlineData("LimitedLiabilityPartnership", PartyLegalStructure.LimitedLiabilityPartnership)]
    [InlineData("ScottishLimitedPartnership", PartyLegalStructure.ScottishLimitedPartnership)]
    [InlineData("LimitedPartnership", PartyLegalStructure.LimitedPartnership)]
    [InlineData("PrivateLimitedCompany", PartyLegalStructure.PrivateLimitedCompany)]
    [InlineData("PublicLimitedCompany", PartyLegalStructure.PublicLimitedCompany)]
    [InlineData("Sole(SoleTrader)", PartyLegalStructure.SoleTrader)]
    public void Every_bian_name_reads_and_is_answered_as_written(string name, PartyLegalStructure structure)
    {
        PartyLegalStructures.Read(name).Value.ShouldBe(structure);
        structure.ToBianName().ShouldBe(name);
    }

    [Theory]
    [InlineData("co-operative", PartyLegalStructure.CoOperative)]
    [InlineData("CoOperative", PartyLegalStructure.CoOperative)]
    [InlineData("SOLE(SOLETRADER)", PartyLegalStructure.SoleTrader)]
    [InlineData(" private limited company ", PartyLegalStructure.PrivateLimitedCompany)]
    public void Case_spaces_and_hyphens_do_not_matter(string text, PartyLegalStructure structure) =>
        PartyLegalStructures.Read(text).Value.ShouldBe(structure);

    [Fact]
    public void The_bian_list_has_twelve_names() =>
        PartyLegalStructures.BianNames.Count.ShouldBe(Enum.GetValues<PartyLegalStructure>().Length);

    [Theory]
    [InlineData("SoleTrader")]
    [InlineData("Trust")]
    [InlineData("4")]
    public void Another_value_is_invalid(string text) =>
        PartyLegalStructures.Read(text).Error.Code.ShouldBe("party.reference-invalid");
}
