namespace RimBank.Party.PartyReferenceDataDirectory.Mcp.Tests;

// Roadmap 1.17: party_reference_data_directory_update answers as PUT …/{id}/Update does: the same body with the ETag as
// version, and the same Problem Details (ADR 0039, ADR 0040).
public sealed class UpdatePartyReferenceDataDirectoryEntryParityTests(PostgresqlServer server)
{
    private static readonly string[] _ids = ["PartyReferenceDataDirectoryEntryId", "DemographicsId", "ReferenceId", "BankRelationsId"];

    [Fact]
    public async Task Update_gives_the_http_body_and_the_etag_as_version()
    {
        await using var host = await server.StartHostAsync();
        var (overHttp, httpVersion) = await PartyTools.RegisterAsync(host, "Person");
        var (overMcp, mcpVersion) = await PartyTools.RegisterAsync(host, "Person");
        const string body = """{ "PartyType": "Organisation" }""";

        var http = await Parity.HttpAsync(host, HttpMethod.Put, Url(overHttp), 200, body, PartyTools.Key("http"), httpVersion);
        var tool = await UpdatePartyReferenceDataDirectoryEntryToolTests.UpdateAsync(host, overMcp, body, mcpVersion, PartyTools.Key("mcp"));

        await Parity.ShouldMirrorAsync(http, tool, _ids);
    }

    [Theory]
    [InlineData("""{ "PartyType": "Alien" }""", 400)]
    [InlineData("""{ "PartyType": "" }""", 400)]
    public async Task An_invalid_party_type_is_the_same_problem(string body, int status)
    {
        await using var host = await server.StartHostAsync();
        var (id, version) = await PartyTools.RegisterAsync(host, "Person");

        var http = await Parity.HttpAsync(host, HttpMethod.Put, Url(id), status, body, PartyTools.Key("http"), version);
        var tool = await UpdatePartyReferenceDataDirectoryEntryToolTests.UpdateAsync(host, id, body, version, PartyTools.Key("mcp"));

        await Parity.ShouldBeSameProblemAsync(http, tool);
    }

    [Fact]
    public async Task A_stale_version_is_the_same_problem()
    {
        await using var host = await server.StartHostAsync();
        var (id, version) = await PartyTools.RegisterAsync(host, "Person");
        (await UpdatePartyReferenceDataDirectoryEntryToolTests.UpdateAsync(host, id, "{}", version, PartyTools.Key("first"))).Success();

        var http = await Parity.HttpAsync(host, HttpMethod.Put, Url(id), 409, "{}", PartyTools.Key("http"), version);
        var tool = await UpdatePartyReferenceDataDirectoryEntryToolTests.UpdateAsync(host, id, "{}", version, PartyTools.Key("mcp"));

        await Parity.ShouldBeSameProblemAsync(http, tool);
    }

    [Fact]
    public async Task An_unknown_entry_is_the_same_problem()
    {
        await using var host = await server.StartHostAsync();
        var id = Guid.CreateVersion7();

        var http = await Parity.HttpAsync(host, HttpMethod.Put, Url(id), 404, "{}", PartyTools.Key("http"), 1);
        var tool = await UpdatePartyReferenceDataDirectoryEntryToolTests.UpdateAsync(host, id, "{}", 1, PartyTools.Key("mcp"));

        await Parity.ShouldBeSameProblemAsync(http, tool);
    }

    private static string Url(Guid id) => $"/PartyReferenceDataDirectory/{id}/Update";
}
