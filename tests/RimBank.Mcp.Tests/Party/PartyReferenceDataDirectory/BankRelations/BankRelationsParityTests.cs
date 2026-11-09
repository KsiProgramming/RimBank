namespace RimBank.Party.PartyReferenceDataDirectory.Mcp.Tests;

// Roadmap 1.17: the BankRelations tools answer as their HTTP endpoints do: the same body with the ETag as version, and
// the same Problem Details (ADR 0039, ADR 0040).
public sealed class BankRelationsParityTests(PostgresqlServer server)
{
    private static readonly string[] _ids = ["PartyReferenceDataDirectoryEntryId", "BankRelationsId"];

    [Fact]
    public async Task Update_gives_the_http_body_and_the_etag_as_version()
    {
        await using var host = await server.StartHostAsync();
        var manager = await Entry.RegisterAsync(host, "Person");
        var overHttp = await Entry.RegisterAsync(host);
        var overMcp = await Entry.RegisterAsync(host);
        var body = BankRelationsToolTests.Managed(manager.Id, "AccountOfficer");

        var http = await Parity.HttpAsync(host, HttpMethod.Put, Url(overHttp, "Update"), 200, body, PartyTools.Key("http"), overHttp.Version);
        var tool = await BankRelationsToolTests.UpdateAsync(host, overMcp, body, overMcp.Version, PartyTools.Key("mcp"));

        await Parity.ShouldMirrorAsync(http, tool, _ids);
    }

    [Fact]
    public async Task Retrieve_gives_the_http_body_and_the_etag_as_version()
    {
        await using var host = await server.StartHostAsync();
        var unit = await Entry.RegisterAsync(host, "Organisation");
        var customer = await Entry.RegisterAsync(host);
        (await BankRelationsToolTests.UpdateAsync(
            host, customer, BankRelationsToolTests.Managed(unit.Id, "ServicingBusinessUnit"), customer.Version, PartyTools.Key("unit"))).Success();

        var http = await Parity.HttpAsync(host, HttpMethod.Get, Url(customer, "Retrieve"), 200);
        var tool = await BankRelationsToolTests.RetrieveAsync(host, customer.Id.ToString(), customer.BankRelationsId.ToString());

        await Parity.ShouldMirrorAsync(http, tool);
    }

    [Theory]
    [InlineData("""{ "BankRelationReference": { "RelationshipType": "Primary", "RelationshipStartDate": { "TimeZoneCode": "CET" } } }""", 400)]
    [InlineData("""{ "BankRelationReference": { "RelationshipStartDate": { "DateTimeContent": "2025-01-02" }, "RelationshipEndDate": { "DateTimeContent": "2025-01-01" } } }""", 400)]
    [InlineData("""{ "BankRelationType": "Customer" }""", 400)]
    public async Task A_refused_part_or_invalid_value_is_the_same_problem(string body, int status)
    {
        await using var host = await server.StartHostAsync();
        var customer = await Entry.RegisterAsync(host);

        var http = await Parity.HttpAsync(host, HttpMethod.Put, Url(customer, "Update"), status, body, PartyTools.Key("http"), customer.Version);
        var tool = await BankRelationsToolTests.UpdateAsync(host, customer, body, customer.Version, PartyTools.Key("mcp"));

        await Parity.ShouldBeSameProblemAsync(http, tool);
    }

    [Fact]
    public async Task A_wrong_type_reference_is_the_same_problem()
    {
        await using var host = await server.StartHostAsync();
        var customer = await Entry.RegisterAsync(host);
        var person = await Entry.RegisterAsync(host, "Person");
        var body = BankRelationsToolTests.Managed(person.Id, "ServicingBranch");

        var http = await Parity.HttpAsync(host, HttpMethod.Put, Url(customer, "Update"), 422, body, PartyTools.Key("http"), customer.Version);
        var tool = await BankRelationsToolTests.UpdateAsync(host, customer, body, customer.Version, PartyTools.Key("mcp"));

        await Parity.ShouldBeSameProblemAsync(http, tool);
    }

    [Theory]
    [InlineData("not-a-guid", 400)]
    [InlineData("01a1b031-8dc0-74cc-a63a-f954f0f3f1f4", 404)]
    public async Task A_malformed_or_unknown_bank_relations_id_is_the_same_problem(string bankRelationsId, int status)
    {
        await using var host = await server.StartHostAsync();
        var customer = await Entry.RegisterAsync(host);

        var http = await Parity.HttpAsync(host, HttpMethod.Get, $"/PartyReferenceDataDirectory/{customer.Id}/BankRelations/{bankRelationsId}/Retrieve", status);
        var tool = await BankRelationsToolTests.RetrieveAsync(host, customer.Id.ToString(), bankRelationsId);

        await Parity.ShouldBeSameProblemAsync(http, tool);
    }

    private static string Url(Entry entry, string operation) =>
        $"/PartyReferenceDataDirectory/{entry.Id}/BankRelations/{entry.BankRelationsId}/{operation}";
}
