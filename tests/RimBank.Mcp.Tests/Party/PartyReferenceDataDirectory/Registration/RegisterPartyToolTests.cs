using RimBank.Host;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp.Tests;

// Roadmap 1.16: party_reference_data_directory_register sends the same RegisterParty as the HTTP endpoint, with the same
// idempotency and errors (specs/tech-stack.md §6, ADR 0039).
public sealed class RegisterPartyToolTests(PostgresqlServer server)
{
    private static readonly string[] _ids = ["PartyReferenceDataDirectoryEntryId", "DemographicsId", "ReferenceId", "BankRelationsId"];

    [Fact]
    public async Task Register_answers_the_new_entry_with_its_version()
    {
        await using var host = await server.StartHostAsync();

        var entry = (await RegisterAsync(host, "Person", "register-1")).Success();

        entry.GetProperty("PartyType").GetString().ShouldBe("Person");
        entry.GetProperty("DirectoryEntryDateType").GetString().ShouldBe("OpenDate");
        entry.GetProperty("DirectoryEntryDate").GetDateTimeOffset().ShouldBe(PostgresqlServer.Now);
        entry.GetProperty("version").GetInt64().ShouldBe(1);
        _ids.Select(name => entry.GetProperty(name).GetGuid())
            .ShouldAllBe(id => id != Guid.Empty && id.Version == 7);
        (await host.ScalarAsync<string>(
                $"SELECT type FROM party_reference_data_directory.mt_events WHERE stream_id = '{entry.GetProperty("PartyReferenceDataDirectoryEntryId").GetGuid()}'"))
            .ShouldBe("party_registered");
    }

    [Fact]
    public async Task The_same_key_and_arguments_replay_the_first_answer()
    {
        await using var host = await server.StartHostAsync();

        var first = (await RegisterAsync(host, "Organisation", "register-twice")).Success();
        var second = (await RegisterAsync(host, "Organisation", "register-twice")).Success();

        second.GetRawText().ShouldBe(first.GetRawText());
        (await host.ScalarAsync<long>(PartyMcpHost.Streams)).ShouldBe(1);
    }

    [Fact]
    public async Task The_same_key_with_other_arguments_is_a_conflict()
    {
        await using var host = await server.StartHostAsync();
        (await RegisterAsync(host, "Person", "register-conflict")).Success();

        (await RegisterAsync(host, "Organisation", "register-conflict")).Problem(409, "shared.idempotency-key-reused");

        (await host.ScalarAsync<long>(PartyMcpHost.Streams)).ShouldBe(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    public async Task A_blank_or_invalid_idempotency_key_is_refused(string key)
    {
        await using var host = await server.StartHostAsync();

        (await RegisterAsync(host, "Person", key)).Problem(400, "shared.idempotency-key-invalid");

        await NothingStoredAsync(host);
    }

    [Fact]
    public async Task A_too_long_idempotency_key_is_refused()
    {
        await using var host = await server.StartHostAsync();

        (await RegisterAsync(host, "Person", new string('k', 256))).Problem(400, "shared.idempotency-key-invalid");

        await NothingStoredAsync(host);
    }

    [Theory]
    [InlineData("", "party.party-type-required")]
    [InlineData("Alien", "party.party-type-invalid")]
    [InlineData("person", "party.party-type-invalid")]
    public async Task A_blank_or_unknown_party_type_is_a_validation_problem(string partyType, string errorCode)
    {
        await using var host = await server.StartHostAsync();

        var problem = (await RegisterAsync(host, partyType, "register-invalid")).Problem(400, errorCode);

        problem.GetProperty("errors").GetProperty("PartyType").GetArrayLength().ShouldBe(1);
        await NothingStoredAsync(host);
    }

    [Theory]
    [InlineData("partyType")]
    [InlineData("idempotencyKey")]
    public async Task A_missing_argument_is_a_tool_arguments_problem(string missing)
    {
        await using var host = await server.StartHostAsync();
        var arguments = new Dictionary<string, object?> { ["partyType"] = "Person", ["idempotencyKey"] = "register-missing" };
        arguments.Remove(missing);

        (await host.CallAsync(RegisterPartyTool.Name, arguments)).Problem(400, "shared.tool-arguments-invalid");

        await NothingStoredAsync(host);
    }

    [Fact]
    public async Task A_caller_with_only_the_read_scope_neither_sees_nor_registers()
    {
        await using var host = await server.StartHostAsync();
        var client = await host.ConnectAsync([PartyReferenceDataDirectoryModule.ReadScope]);

        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);
        var result = await host.CallAsync(
            RegisterPartyTool.Name,
            new() { ["partyType"] = "Person", ["idempotencyKey"] = "register-denied" },
            [PartyReferenceDataDirectoryModule.ReadScope]);

        tools.Select(tool => tool.Name).ShouldNotContain(RegisterPartyTool.Name);
        result.Problem(403, "shared.forbidden");
        await NothingStoredAsync(host);
    }

    internal static Task<ModelContextProtocol.Protocol.CallToolResult> RegisterAsync(PartyMcpHost host, string partyType, string key) =>
        host.CallAsync(RegisterPartyTool.Name, new() { ["partyType"] = partyType, ["idempotencyKey"] = key });

    private static async Task NothingStoredAsync(PartyMcpHost host)
    {
        (await host.ScalarAsync<long>(PartyMcpHost.Streams)).ShouldBe(0);
        (await host.ScalarAsync<long>(PartyMcpHost.Outcomes)).ShouldBe(0);
    }
}
