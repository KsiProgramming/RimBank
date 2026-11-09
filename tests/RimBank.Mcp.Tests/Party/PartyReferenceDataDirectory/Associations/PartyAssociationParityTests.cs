namespace RimBank.Party.PartyReferenceDataDirectory.Mcp.Tests;

// Roadmap 1.17: the Associations tools answer as their HTTP endpoints do: the same body with the association's ETag as
// version, and the same Problem Details (ADR 0039, ADR 0040).
public sealed class PartyAssociationParityTests(PostgresqlServer server)
{
    private const string Period = """, "AssociationValidFromToDate": { "FromDateTime": "2024-01-01", "ToDateTime": "2030-12-31T12:00:00Z" } }""";

    private static readonly string[] _ids = ["AssociationsId"];

    [Fact]
    public async Task Register_gives_the_http_body_and_the_etag_as_version()
    {
        await using var host = await server.StartHostAsync();
        var owner = await Entry.RegisterAsync(host);
        var associate = await Entry.RegisterAsync(host);
        var body = WithPeriod(associate.Id, "PartyActsOnBehalfOfParty");

        var http = await Parity.HttpAsync(host, HttpMethod.Post, $"/PartyReferenceDataDirectory/{owner.Id}/Associations/Register", 200, body, PartyTools.Key("http"));
        var tool = await PartyAssociationToolTests.RegisterAsync(host, owner.Id, body, PartyTools.Key("mcp"));

        await Parity.ShouldMirrorAsync(http, tool, _ids);
    }

    [Fact]
    public async Task Update_and_retrieve_give_the_http_body_and_the_etag_as_version()
    {
        await using var host = await server.StartHostAsync();
        var owner = await Entry.RegisterAsync(host);
        var associate = await Entry.RegisterAsync(host);
        var overHttp = await RegisterAsync(host, owner, associate);
        var overMcp = await RegisterAsync(host, owner, associate);
        const string body = """{ "AssociateType": "PartyIsContactPointForParty", "AssociationValidFromToDate": { "ToDateTime": "2031-01-01" } }""";

        var http = await Parity.HttpAsync(host, HttpMethod.Put, Url(owner, overHttp, "Update"), 200, body, PartyTools.Key("http"), 1);
        var tool = await PartyAssociationToolTests.UpdateAsync(host, owner.Id, overMcp, body, 1, PartyTools.Key("mcp"));
        await Parity.ShouldMirrorAsync(http, tool, _ids);

        var httpRetrieve = await Parity.HttpAsync(host, HttpMethod.Get, Url(owner, overMcp, "Retrieve"), 200);
        var toolRetrieve = await PartyAssociationToolTests.RetrieveAsync(host, owner.Id.ToString(), overMcp.ToString());
        await Parity.ShouldMirrorAsync(httpRetrieve, toolRetrieve);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "AssociateType": "PartyIsFriendOfParty", "AssociateReference": { "PartyReference": "x" } }""")]
    [InlineData("""{ "AssociateReference": { "PartyReference": "x", "InvolvementReference": "Guarantor" }, "productinstancereference": { "ProductInstanceReference": "P-1" } }""")]
    public async Task An_invalid_or_refused_registration_is_the_same_problem(string body)
    {
        await using var host = await server.StartHostAsync();
        var owner = await Entry.RegisterAsync(host);

        var http = await Parity.HttpAsync(host, HttpMethod.Post, $"/PartyReferenceDataDirectory/{owner.Id}/Associations/Register", 400, body, PartyTools.Key("http"));
        var tool = await PartyAssociationToolTests.RegisterAsync(host, owner.Id, body, PartyTools.Key("mcp"));

        await Parity.ShouldBeSameProblemAsync(http, tool);
    }

    [Fact]
    public async Task An_unregistered_associate_is_the_same_problem()
    {
        await using var host = await server.StartHostAsync();
        var owner = await Entry.RegisterAsync(host);
        var body = PartyAssociationToolTests.Body(Guid.CreateVersion7(), "PartyIsMarriedToParty");

        var http = await Parity.HttpAsync(host, HttpMethod.Post, $"/PartyReferenceDataDirectory/{owner.Id}/Associations/Register", 422, body, PartyTools.Key("http"));
        var tool = await PartyAssociationToolTests.RegisterAsync(host, owner.Id, body, PartyTools.Key("mcp"));

        await Parity.ShouldBeSameProblemAsync(http, tool);
    }

    [Fact]
    public async Task A_stale_association_version_is_the_same_problem()
    {
        await using var host = await server.StartHostAsync();
        var owner = await Entry.RegisterAsync(host);
        var associate = await Entry.RegisterAsync(host);
        var association = await RegisterAsync(host, owner, associate);
        (await PartyAssociationToolTests.UpdateAsync(host, owner.Id, association, "{}", 1, PartyTools.Key("first"))).Success();

        var http = await Parity.HttpAsync(host, HttpMethod.Put, Url(owner, association, "Update"), 409, "{}", PartyTools.Key("http"), 1);
        var tool = await PartyAssociationToolTests.UpdateAsync(host, owner.Id, association, "{}", 1, PartyTools.Key("mcp"));

        await Parity.ShouldBeSameProblemAsync(http, tool);
    }

    [Theory]
    [InlineData("not-a-guid", 400)]
    [InlineData("01a1b031-8dc0-74cc-a63a-f954f0f3f1f4", 404)]
    public async Task A_malformed_or_unknown_association_id_is_the_same_problem(string associationsId, int status)
    {
        await using var host = await server.StartHostAsync();
        var owner = await Entry.RegisterAsync(host);

        var http = await Parity.HttpAsync(host, HttpMethod.Get, $"/PartyReferenceDataDirectory/{owner.Id}/Associations/{associationsId}/Retrieve", status);
        var tool = await PartyAssociationToolTests.RetrieveAsync(host, owner.Id.ToString(), associationsId);

        await Parity.ShouldBeSameProblemAsync(http, tool);
    }

    private static string WithPeriod(Guid associate, string type) =>
        $$"""{ "AssociateReference": { "PartyReference": "{{associate}}" }, "AssociateType": "{{type}}" """ + Period;

    private static async Task<Guid> RegisterAsync(PartyMcpHost host, Entry owner, Entry associate) =>
        (await PartyAssociationToolTests.RegisterAsync(host, owner.Id, WithPeriod(associate.Id, "PartyIsTrustedByParty"), PartyTools.Key("register")))
            .Success().GetProperty("AssociationsId").GetGuid();

    private static string Url(Entry owner, Guid association, string operation) =>
        $"/PartyReferenceDataDirectory/{owner.Id}/Associations/{association}/{operation}";
}
