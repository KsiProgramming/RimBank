using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp.Tests;

// Roadmap 1.17: the Associations tools send the same Register, Update and Retrieve as the HTTP endpoints. An association
// is its own aggregate, so its version is its own stream's (ADR 0024, ADR 0033, ADR 0039, ADR 0040).
public sealed class PartyAssociationToolTests(PostgresqlServer server)
{
    [Fact]
    public async Task Register_retrieve_then_update_with_the_associations_version()
    {
        await using var host = await server.StartHostAsync();
        var owner = await Entry.RegisterAsync(host, "Organisation");
        var associate = await Entry.RegisterAsync(host, "Organisation");

        var registered = (await RegisterAsync(host, owner.Id, Body(associate.Id, "PartyIsParentOfParty"), PartyTools.Key("register"))).Success();
        var associationId = registered.GetProperty("AssociationsId").GetGuid();
        var retrieved = (await RetrieveAsync(host, owner.Id.ToString(), associationId.ToString())).Success();
        var updated = (await UpdateAsync(
                host,
                owner.Id,
                associationId,
                """{ "AssociateType": "PartyIsSubsidiaryOfParty", "AssociationValidFromToDate": { "FromDateTime": "2024-01-01" } }""",
                retrieved.GetProperty("version").GetInt64(),
                PartyTools.Key("update")))
            .Success();

        registered.GetProperty("version").GetInt64().ShouldBe(1);
        registered.GetProperty("PartyReferenceDataDirectoryEntryId").GetGuid().ShouldBe(owner.Id);
        registered.GetProperty("AssociateReference").GetProperty("PartyReference").GetGuid().ShouldBe(associate.Id);
        JsonElement.DeepEquals(retrieved, registered).ShouldBeTrue($"{retrieved} vs {registered}");
        updated.GetProperty("AssociateType").GetString().ShouldBe("PartyIsSubsidiaryOfParty");
        updated.GetProperty("AssociationValidFromToDate").GetProperty("FromDateTime").GetString().ShouldBe("2024-01-01T00:00:00Z");
        updated.GetProperty("version").GetInt64().ShouldBe(2);
    }

    [Fact]
    public async Task An_unregistered_associate_or_the_party_itself_is_refused()
    {
        await using var host = await server.StartHostAsync();
        var owner = await Entry.RegisterAsync(host);

        (await RegisterAsync(host, owner.Id, Body(Guid.CreateVersion7(), "PartyIsMarriedToParty"), PartyTools.Key("unknown")))
            .Problem(422, "party.associate-not-registered");
        (await RegisterAsync(host, owner.Id, Body(owner.Id, "PartyIsMarriedToParty"), PartyTools.Key("self")))
            .Problem(422, "party.self-association");
    }

    [Fact]
    public async Task A_bian_field_rimbank_does_not_record_is_refused()
    {
        await using var host = await server.StartHostAsync();
        var owner = await Entry.RegisterAsync(host);
        var associate = await Entry.RegisterAsync(host);

        (await RegisterAsync(
                host,
                owner.Id,
                $$"""{ "AssociateReference": { "PartyReference": "{{associate.Id}}", "InvolvementReference": "Guarantor" }, "AssociateType": "PartyIsTrustedByParty" }""",
                PartyTools.Key("refused")))
            .Problem(400, "party.association-field-not-supported");
    }

    [Fact]
    public async Task The_associate_cannot_change_and_a_stale_version_is_a_conflict()
    {
        await using var host = await server.StartHostAsync();
        var owner = await Entry.RegisterAsync(host);
        var associate = await Entry.RegisterAsync(host);
        var other = await Entry.RegisterAsync(host);
        var associationId = (await RegisterAsync(host, owner.Id, Body(associate.Id, "PartyIsTrustedByParty"), PartyTools.Key("register"))).Success()
            .GetProperty("AssociationsId").GetGuid();

        (await UpdateAsync(host, owner.Id, associationId, Body(other.Id, "PartyIsTrustedByParty"), 1, PartyTools.Key("change")))
            .Problem(422, "party.associate-cannot-change");
        (await UpdateAsync(host, owner.Id, associationId, "{}", 1, PartyTools.Key("first"))).Success();
        (await UpdateAsync(host, owner.Id, associationId, "{}", 1, PartyTools.Key("stale")))
            .Problem(409, PartyReferenceDataDirectoryEntryErrors.VersionConflict.Code);
    }

    [Fact]
    public async Task Only_the_owner_finds_the_association()
    {
        await using var host = await server.StartHostAsync();
        var owner = await Entry.RegisterAsync(host);
        var associate = await Entry.RegisterAsync(host);
        var associationId = (await RegisterAsync(host, owner.Id, Body(associate.Id, "PartyIsTrustedByParty"), PartyTools.Key("register"))).Success()
            .GetProperty("AssociationsId").GetGuid();

        (await RetrieveAsync(host, associate.Id.ToString(), associationId.ToString())).Problem(404, "party.association-not-found");
    }

    internal static string Body(Guid associate, string type) =>
        $$"""{ "AssociateReference": { "PartyReference": "{{associate}}" }, "AssociateType": "{{type}}" }""";

    internal static Task<CallToolResult> RegisterAsync(PartyMcpHost host, Guid owner, string request, string key) =>
        host.CallAsync(
            RegisterPartyAssociationTool.Name,
            new()
            {
                ["partyReferenceDataDirectoryEntryId"] = owner.ToString(),
                ["request"] = JsonElement.Parse(request),
                ["idempotencyKey"] = key,
            });

    internal static Task<CallToolResult> UpdateAsync(PartyMcpHost host, Guid owner, Guid association, string request, long expectedVersion, string key) =>
        host.CallAsync(
            UpdatePartyAssociationTool.Name,
            new()
            {
                ["partyReferenceDataDirectoryEntryId"] = owner.ToString(),
                ["associationsId"] = association.ToString(),
                ["request"] = JsonElement.Parse(request),
                ["expectedVersion"] = expectedVersion,
                ["idempotencyKey"] = key,
            });

    internal static Task<CallToolResult> RetrieveAsync(PartyMcpHost host, string owner, string association) =>
        host.CallAsync(
            RetrievePartyAssociationTool.Name,
            new() { ["partyReferenceDataDirectoryEntryId"] = owner, ["associationsId"] = association });
}
