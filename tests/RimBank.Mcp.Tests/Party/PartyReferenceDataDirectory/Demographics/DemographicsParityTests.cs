namespace RimBank.Party.PartyReferenceDataDirectory.Mcp.Tests;

// Roadmap 1.17: the Demographics tools answer as their HTTP endpoints do: the same body with the ETag as version, and
// the same Problem Details (ADR 0039, ADR 0040).
public sealed class DemographicsParityTests(PostgresqlServer server)
{
    private const string Details = """
        {
          "Socio-EconomicClassification": "Professional",
          "Employment": { "PartyReference": "Acme", "InvolvementReference": "Engineer" },
          "EmploymentHistory": "Acme since 2020",
          "EducationHistory": "MSc",
          "ServicingConstraints": "Large print"
        }
        """;

    private static readonly string[] _ids = ["PartyReferenceDataDirectoryEntryId", "DemographicsId"];

    [Fact]
    public async Task Update_gives_the_http_body_and_the_etag_as_version()
    {
        await using var host = await server.StartHostAsync();
        var overHttp = await Entry.RegisterAsync(host);
        var overMcp = await Entry.RegisterAsync(host);

        var http = await Parity.HttpAsync(host, HttpMethod.Put, Url(overHttp, "Update"), 200, Details, PartyTools.Key("http"), overHttp.Version);
        var tool = await DemographicsToolTests.UpdateAsync(host, overMcp, Details, overMcp.Version, PartyTools.Key("mcp"));

        await Parity.ShouldMirrorAsync(http, tool, _ids);
    }

    [Fact]
    public async Task Exchange_gives_the_http_body_and_the_etag_as_version()
    {
        await using var host = await server.StartHostAsync();
        var overHttp = await Entry.RegisterAsync(host);
        var overMcp = await Entry.RegisterAsync(host);
        (await DemographicsToolTests.UpdateAsync(host, overHttp, Details, overHttp.Version, PartyTools.Key("details"))).Success();
        (await DemographicsToolTests.UpdateAsync(host, overMcp, Details, overMcp.Version, PartyTools.Key("details"))).Success();
        const string verify = """{ "ExchangeActionType": "Verify" }""";

        var http = await Parity.HttpAsync(host, HttpMethod.Put, Url(overHttp, "Exchange"), 200, verify, PartyTools.Key("http"), overHttp.Version + 1);
        var tool = await DemographicsToolTests.ExchangeAsync(host, overMcp, verify, overMcp.Version + 1, PartyTools.Key("mcp"));

        await Parity.ShouldMirrorAsync(http, tool, _ids);
    }

    [Fact]
    public async Task Retrieve_gives_the_http_body_and_the_etag_as_version()
    {
        await using var host = await server.StartHostAsync();
        var entry = await Entry.RegisterAsync(host);
        (await DemographicsToolTests.UpdateAsync(host, entry, Details, entry.Version, PartyTools.Key("details"))).Success();

        var http = await Parity.HttpAsync(host, HttpMethod.Get, Url(entry, "Retrieve"), 200);
        var tool = await DemographicsToolTests.RetrieveAsync(host, entry.Id.ToString(), entry.DemographicsId.ToString());

        await Parity.ShouldMirrorAsync(http, tool);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "ExchangeActionType": "Approve" }""")]
    [InlineData("""{ "ExchangeActionType": "Verify" }""")]
    public async Task An_invalid_or_premature_exchange_is_the_same_problem(string request)
    {
        await using var host = await server.StartHostAsync();
        var entry = await Entry.RegisterAsync(host);
        var status = request.Contains("Verify", StringComparison.Ordinal) ? 422 : 400;

        var http = await Parity.HttpAsync(host, HttpMethod.Put, Url(entry, "Exchange"), status, request, PartyTools.Key("http"), entry.Version);
        var tool = await DemographicsToolTests.ExchangeAsync(host, entry, request, entry.Version, PartyTools.Key("mcp"));

        await Parity.ShouldBeSameProblemAsync(http, tool);
    }

    [Fact]
    public async Task An_invalid_detail_is_the_same_problem()
    {
        await using var host = await server.StartHostAsync();
        var entry = await Entry.RegisterAsync(host);
        var tooLong = $$"""{ "EducationHistory": "{{new string('x', 5000)}}" }""";

        var http = await Parity.HttpAsync(host, HttpMethod.Put, Url(entry, "Update"), 400, tooLong, PartyTools.Key("http"), entry.Version);
        var tool = await DemographicsToolTests.UpdateAsync(host, entry, tooLong, entry.Version, PartyTools.Key("mcp"));

        await Parity.ShouldBeSameProblemAsync(http, tool);
    }

    [Theory]
    [InlineData("not-a-guid", 400)]
    [InlineData("01a1b031-8dc0-74cc-a63a-f954f0f3f1f4", 404)]
    public async Task A_malformed_or_unknown_demographics_id_is_the_same_problem(string demographicsId, int status)
    {
        await using var host = await server.StartHostAsync();
        var entry = await Entry.RegisterAsync(host);

        var http = await Parity.HttpAsync(host, HttpMethod.Get, $"/PartyReferenceDataDirectory/{entry.Id}/Demographics/{demographicsId}/Retrieve", status);
        var tool = await DemographicsToolTests.RetrieveAsync(host, entry.Id.ToString(), demographicsId);

        await Parity.ShouldBeSameProblemAsync(http, tool);
    }

    private static string Url(Entry entry, string operation) =>
        $"/PartyReferenceDataDirectory/{entry.Id}/Demographics/{entry.DemographicsId}/{operation}";
}
