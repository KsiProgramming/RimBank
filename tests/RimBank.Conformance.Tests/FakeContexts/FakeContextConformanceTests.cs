using RimBank.Conformance.Testing;

namespace RimBank.Conformance.FakeContexts.Tests;

// Roadmap 0.14: the harness passes a conforming fake context and fails each kind of non-conformance with a message
// that names the BIAN operation, route or channel (ADR 0020). The fake maps routes against the real Party spec.
public sealed class FakeContextConformanceTests
{
    private const string Register = "PartyReferenceDataDirectoryEntry/Register";
    private const string Retrieve = "PartyReferenceDataDirectoryEntry/Retrieve";
    private const string Update = "PartyReferenceDataDirectoryEntry/Update";

    private static readonly (string, string) _register = ("POST", "/PartyReferenceDataDirectory/Register");
    private static readonly (string, string) _retrieve = ("GET", "/PartyReferenceDataDirectory/{id}/Retrieve");
    private static readonly (string, string) _update = ("PUT", "/PartyReferenceDataDirectory/{id}/Update");

    private static Task<BianServiceDomainSpecification> Party => BianSpecifications.LoadAsync("PartyReferenceDataDirectory");

    [Fact]
    public async Task A_conforming_subset_passes()
    {
        var routes = await FakeContext.MapAsync(_register, _retrieve, _update);

        routes.ShouldConformTo(await Party, [Register, Retrieve, Update]);
    }

    [Fact]
    public async Task Routes_outside_the_service_domain_are_ignored()
    {
        var routes = await FakeContext.MapAsync(_register, ("GET", "/health"), ("POST", "/mcp"), ("POST", "/CurrentAccount/Initiate"));

        routes.ShouldConformTo(await Party, [Register]);
    }

    [Fact]
    public async Task A_declared_operation_that_is_not_mapped_fails()
    {
        var routes = await FakeContext.MapAsync(_register);

        var failure = Should.Throw<ShouldAssertException>(async () => routes.ShouldConformTo(await Party, [Register, Retrieve]));

        failure.Message.ShouldContain($"The declared operation {Retrieve} is not mapped");
        failure.Message.ShouldContain("GET /PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Retrieve");
    }

    [Fact]
    public async Task A_declared_operation_that_is_not_in_the_spec_fails()
    {
        var routes = await FakeContext.MapAsync(_register);

        var failure = Should.Throw<ShouldAssertException>(async () => routes.ShouldConformTo(await Party, [Register, "PartyReferenceDataDirectoryEntry/Archive"]));

        failure.Message.ShouldContain("'PartyReferenceDataDirectoryEntry/Archive' is not an operation of the BIAN specification");
    }

    [Fact]
    public async Task A_command_with_the_wrong_method_fails()
    {
        var routes = await FakeContext.MapAsync(("POST", "/PartyReferenceDataDirectory/{id}/Update"));

        var failure = Should.Throw<ShouldAssertException>(async () => routes.ShouldConformTo(await Party, []));

        failure.Message.ShouldContain($"{Update} is mapped as POST /PartyReferenceDataDirectory/{{id}}/Update");
        failure.Message.ShouldContain("declares PUT");
    }

    [Fact]
    public async Task A_retrieve_mapped_as_a_command_fails_the_command_query_rule()
    {
        var routes = await FakeContext.MapAsync(("POST", "/PartyReferenceDataDirectory/{id}/Retrieve"));

        var failure = Should.Throw<ShouldAssertException>(async () => routes.ShouldConformTo(await Party, []));

        failure.Message.ShouldContain($"{Retrieve} is a query (Retrieve) but is mapped as POST");
        failure.Message.ShouldContain("command/query rule");
    }

    [Fact]
    public async Task A_command_mapped_as_GET_fails_the_command_query_rule()
    {
        var routes = await FakeContext.MapAsync(("GET", "/PartyReferenceDataDirectory/{id}/Update"));

        var failure = Should.Throw<ShouldAssertException>(async () => routes.ShouldConformTo(await Party, []));

        failure.Message.ShouldContain($"{Update} is a command but is mapped as GET");
        failure.Message.ShouldContain("command/query rule");
    }

    [Fact]
    public async Task A_route_that_is_not_in_the_spec_fails()
    {
        var routes = await FakeContext.MapAsync(("POST", "/PartyReferenceDataDirectory/{id}/Archive"));

        var failure = Should.Throw<ShouldAssertException>(async () => routes.ShouldConformTo(await Party, []));

        failure.Message.ShouldContain("POST /PartyReferenceDataDirectory/{id}/Archive is not an operation of the BIAN specification");
    }

    [Fact]
    public async Task Every_problem_is_reported_in_one_failure()
    {
        var routes = await FakeContext.MapAsync(("GET", "/PartyReferenceDataDirectory/{id}/Update"), ("POST", "/PartyReferenceDataDirectory/{id}/Archive"));

        var failure = Should.Throw<ShouldAssertException>(async () => routes.ShouldConformTo(await Party, [Register]));

        failure.Message.ShouldContain("failed with 3 problem(s)");
        failure.Message.ShouldContain($"{Update} is a command");
        failure.Message.ShouldContain("/Archive is not an operation");
        failure.Message.ShouldContain($"The declared operation {Register} is not mapped");
    }

    [Fact]
    public async Task A_published_channel_of_the_spec_passes()
    {
        string[] published = ["PartyReferenceDataDirectoryEntry/Created", "PartyReferenceDataDirectoryEntry/Updated"];

        published.ShouldBeChannelsOf(await Party);
    }

    [Fact]
    public async Task A_published_channel_that_is_not_in_the_spec_fails()
    {
        string[] published = ["PartyReferenceDataDirectoryEntry/Created", "PartyReferenceDataDirectoryEntry/Deleted"];

        var failure = Should.Throw<ShouldAssertException>(async () => published.ShouldBeChannelsOf(await Party));

        failure.Message.ShouldContain("failed with 1 problem(s)");
        failure.Message.ShouldContain("'PartyReferenceDataDirectoryEntry/Deleted' is not in the BIAN AsyncAPI specification");
    }
}
