using RimBank.Conformance.Testing;

namespace RimBank.Conformance.Tests;

// ADR 0020: routes match BIAN paths whatever the parameter names, constraints or letter case.
public sealed class RouteTemplateTests
{
    private const string BianRetrieve = "/PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Retrieve";

    [Theory]
    [InlineData("/PartyReferenceDataDirectory/{id}/Retrieve")]
    [InlineData("/PartyReferenceDataDirectory/{id:guid}/Retrieve")]
    [InlineData("/PartyReferenceDataDirectory/{id?}/Retrieve")]
    [InlineData("PartyReferenceDataDirectory/{id}/Retrieve/")]
    [InlineData("/partyreferencedatadirectory/{id}/retrieve")]
    public void A_route_matches_the_BIAN_path(string route)
    {
        RouteTemplate.Parse(route).ShouldBe(RouteTemplate.Parse(BianRetrieve));
    }

    [Theory]
    [InlineData("/PartyReferenceDataDirectory/{id}/Update")]
    [InlineData("/PartyReferenceDataDirectory/Retrieve")]
    [InlineData("/PartyReferenceDataDirectory/{id}/Retrieve/{other}")]
    [InlineData("/PartyReferenceDataDirectory/latest/Retrieve")]
    public void A_different_route_does_not_match(string route)
    {
        RouteTemplate.Parse(route).ShouldNotBe(RouteTemplate.Parse(BianRetrieve));
    }

    [Fact]
    public void The_template_keeps_its_text_and_first_segment()
    {
        var template = RouteTemplate.Parse(BianRetrieve);

        template.Text.ShouldBe(BianRetrieve);
        template.ToString().ShouldBe(BianRetrieve);
        template.FirstSegment.ShouldBe("PartyReferenceDataDirectory");
    }
}
