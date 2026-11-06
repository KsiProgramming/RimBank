namespace RimBank.Postgresql.Tests;

public sealed class ServiceDomainNameTests
{
    [Fact]
    public void A_service_domain_gives_its_schema_role_connection_string_and_health_check_names()
    {
        var name = ServiceDomainName.Parse("PartyReferenceDataDirectory");

        name.SchemaName.ShouldBe("party_reference_data_directory");
        name.RoleName.ShouldBe("rimbank_party_reference_data_directory");
        name.ConnectionStringName.ShouldBe("PartyReferenceDataDirectory");
        name.HealthCheckName.ShouldBe("postgresql-party-reference-data-directory");
        name.ToString().ShouldBe("PartyReferenceDataDirectory");
    }

    [Theory]
    [InlineData("")]
    [InlineData("partyReferenceDataDirectory")]
    [InlineData("Party_Reference")]
    [InlineData("Party-Reference")]
    [InlineData("Party Reference")]
    [InlineData("Party1")]
    [InlineData("PartyRéférence")]
    [InlineData("party\"; DROP ROLE postgres; --")]
    public void Anything_but_a_PascalCase_name_is_rejected(string value)
    {
        Should.Throw<ArgumentException>(() => ServiceDomainName.Parse(value));
    }

    [Fact]
    public void A_name_whose_role_would_be_truncated_by_PostgreSQL_is_rejected()
    {
        var tooLong = string.Concat(Enumerable.Repeat("Ledger", 9));

        Should.Throw<ArgumentException>(() => ServiceDomainName.Parse(tooLong)).Message.ShouldContain("63");
    }
}
