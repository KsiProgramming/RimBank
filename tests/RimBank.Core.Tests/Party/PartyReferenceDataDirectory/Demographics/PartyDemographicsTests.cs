using static RimBank.Party.PartyReferenceDataDirectory.Tests.EntryExamples;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The party's demographic details and the partial update (roadmap 1.9).
public sealed class PartyDemographicsTests
{
    [Fact]
    public void Empty_details_have_no_details()
    {
        PartyDemographics.Empty.HasDetails.ShouldBeFalse();
        Details.HasDetails.ShouldBeTrue();
    }

    [Fact]
    public void No_change_keeps_every_detail() =>
        Details.Apply(DemographicsChanges.None).ShouldBe(Details);

    [Fact]
    public void A_given_field_replaces_only_that_detail() =>
        Details.Apply(DemographicsChanges.None with { EducationHistory = "PhD" })
            .ShouldBe(Details with { EducationHistory = "PhD" });

    [Fact]
    public void An_empty_field_clears_the_detail() =>
        Details.Apply(DemographicsChanges.None with { ServicingConstraints = "" })
            .ShouldBe(Details with { ServicingConstraints = null });

    [Fact]
    public void A_given_employment_replaces_the_whole_employment() =>
        Details.Apply(DemographicsChanges.None with { Employment = new Employment("Fabrikam", null) })
            .Employment.ShouldBe(new Employment("Fabrikam", null));

    [Fact]
    public void An_empty_employment_clears_it() =>
        Details.Apply(DemographicsChanges.None with { Employment = new Employment(null, null) })
            .Employment.ShouldBeNull();

    [Fact]
    public void Clearing_every_detail_leaves_no_details() =>
        Details.Apply(new DemographicsChanges("", new Employment(null, null), "", "", "")).HasDetails.ShouldBeFalse();

    [Fact]
    public void An_update_that_changes_nothing_keeps_the_verification()
    {
        var verified = new DemographicsRecord(Details, DemographicsVerificationStatus.Verified, VerificationDate);

        verified.UpdatedTo(Details).ShouldBe(verified);
    }

    [Fact]
    public void An_update_that_changes_the_details_makes_them_unverified()
    {
        var verified = new DemographicsRecord(Details, DemographicsVerificationStatus.Verified, VerificationDate);

        verified.UpdatedTo(Details with { SocioEconomicClassification = "C1" })
            .ShouldBe(new DemographicsRecord(Details with { SocioEconomicClassification = "C1" }, DemographicsVerificationStatus.Unverified, null));
    }
}
