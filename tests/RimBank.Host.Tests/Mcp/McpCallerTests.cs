using ModelContextProtocol.Protocol;

namespace RimBank.Host.Mcp.Tests;

// Roadmap 0.16: a tool call carries its caller to the message bus as an HTTP call does, so command idempotency is
// scoped to the caller and the audit trail can name it (specs/tech-stack.md §6 "Audit", ADR 0019, ADR 0022).
public sealed class McpCallerTests
{
    [Fact]
    public async Task A_tool_call_relays_the_callers_subject_to_the_message_bus()
    {
        await using var app = await McpTestHost.StartAsync(new McpCallerServiceDomainModule());
        await using var client = await McpTestHost.ConnectAsync(app);

        var result = await client.CallToolAsync(FakeCallerServiceDomainTools.ExecuteName, cancellationToken: TestContext.Current.CancellationToken);

        result.IsError.ShouldNotBe(true);
        result.Content.OfType<TextContentBlock>().ShouldHaveSingleItem().Text.ShouldBe(TestTokens.Subject);
    }
}
