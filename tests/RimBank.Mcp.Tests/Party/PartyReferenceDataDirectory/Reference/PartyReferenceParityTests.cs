namespace RimBank.Party.PartyReferenceDataDirectory.Mcp.Tests;

// Roadmap 1.17: the Reference tools answer as their HTTP endpoints do: the same body with the ETag as version, and the
// same Problem Details (ADR 0039, ADR 0040).
public sealed class PartyReferenceParityTests(PostgresqlServer server)
{
    private static readonly string[] _ids = ["PartyReferenceDataDirectoryEntryId", "ReferenceId"];

    [Fact]
    public async Task Update_of_every_person_field_gives_the_http_body_and_the_etag_as_version()
    {
        await using var host = await server.StartHostAsync();
        var overHttp = await Entry.RegisterAsync(host);
        var overMcp = await Entry.RegisterAsync(host);

        var http = await Parity.HttpAsync(host, HttpMethod.Put, Url(overHttp, "Update"), 200, PartyReferenceToolTests.Person, PartyTools.Key("http"), overHttp.Version);
        var tool = await PartyReferenceToolTests.UpdateAsync(host, overMcp, PartyReferenceToolTests.Person, overMcp.Version, PartyTools.Key("mcp"));

        await Parity.ShouldMirrorAsync(http, tool, _ids);
    }

    [Fact]
    public async Task Update_of_an_organisations_corporate_fields_gives_the_http_body()
    {
        await using var host = await server.StartHostAsync();
        var group = await Entry.RegisterAsync(host, "Organisation");
        var officer = await Entry.RegisterAsync(host, "Person");
        var overHttp = await Entry.RegisterAsync(host, "Organisation");
        var overMcp = await Entry.RegisterAsync(host, "Organisation");
        var body = $$"""
            {
              "PartyLegalEntityReference": { "Identification": "5493001kjtiigc8y1r12" },
              "PartyFullLegalName": "Dupont SA",
              "PartyLegalStructure": "private limited company",
              "CorporateAddress": { "Department": "Finance", "StreetName": "Avenue Louise", "TownName": "Ixelles", "Country": { "Code": "BE" } },
              "CorporateCustomerReference": { "PartyReference": "{{group.Id}}" },
              "CorporateCustomerLegalEntityReference": { "PartyReference": "{{group.Id}}" },
              "CompanyOfficerReference": { "PartyReference": "{{officer.Id}}" },
              "CompanyOfficerRole": " Director "
            }
            """;

        var http = await Parity.HttpAsync(host, HttpMethod.Put, Url(overHttp, "Update"), 200, body, PartyTools.Key("http"), overHttp.Version);
        var tool = await PartyReferenceToolTests.UpdateAsync(host, overMcp, body, overMcp.Version, PartyTools.Key("mcp"));

        await Parity.ShouldMirrorAsync(http, tool, _ids);
    }

    [Fact]
    public async Task Retrieve_gives_the_http_body_and_the_etag_as_version()
    {
        await using var host = await server.StartHostAsync();
        var entry = await Entry.RegisterAsync(host);
        (await PartyReferenceToolTests.UpdateAsync(host, entry, PartyReferenceToolTests.Person, entry.Version, PartyTools.Key("person"))).Success();

        var http = await Parity.HttpAsync(host, HttpMethod.Get, Url(entry, "Retrieve"), 200);
        var tool = await PartyReferenceToolTests.RetrieveAsync(host, entry.Id.ToString(), entry.ReferenceId.ToString());

        await Parity.ShouldMirrorAsync(http, tool);
    }

    [Theory]
    [InlineData("""{ "PartyLegalEntityReference": { "Identification": "5493001KJTIIGC8Y1R13" }, "DateofBirth": "1850-01-01" }""", 400)]
    [InlineData("""{ "eMailAddress": { "EmailAddress": "jeanne@example.fr", "Identification": "x" }, "PartyAddress": { "TownName": "Paris", "Country": { "Code": "FR", "Name": "France" } } }""", 400)]
    [InlineData("""{ "CompanyOfficerRole": { "PartyName": "x" } }""", 400)]
    [InlineData("""{ "CorporateCustomerReference": { "PartyReference": "01a1b031-8dc0-74cc-a63a-f954f0f3f1f4" } }""", 422)]
    public async Task An_invalid_value_or_refused_part_is_the_same_problem(string body, int status)
    {
        await using var host = await server.StartHostAsync();
        var entry = await Entry.RegisterAsync(host, "Organisation");

        var http = await Parity.HttpAsync(host, HttpMethod.Put, Url(entry, "Update"), status, body, PartyTools.Key("http"), entry.Version);
        var tool = await PartyReferenceToolTests.UpdateAsync(host, entry, body, entry.Version, PartyTools.Key("mcp"));

        await Parity.ShouldBeSameProblemAsync(http, tool);
    }

    [Theory]
    [InlineData("not-a-guid", 400)]
    [InlineData("01a1b031-8dc0-74cc-a63a-f954f0f3f1f4", 404)]
    public async Task A_malformed_or_unknown_reference_id_is_the_same_problem(string referenceId, int status)
    {
        await using var host = await server.StartHostAsync();
        var entry = await Entry.RegisterAsync(host);

        var http = await Parity.HttpAsync(host, HttpMethod.Get, $"/PartyReferenceDataDirectory/{entry.Id}/Reference/{referenceId}/Retrieve", status);
        var tool = await PartyReferenceToolTests.RetrieveAsync(host, entry.Id.ToString(), referenceId);

        await Parity.ShouldBeSameProblemAsync(http, tool);
    }

    private static string Url(Entry entry, string operation) =>
        $"/PartyReferenceDataDirectory/{entry.Id}/Reference/{entry.ReferenceId}/{operation}";
}
