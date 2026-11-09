using System.Text.Json;
using Microsoft.AspNetCore.Http;
using RimBank.Conformance.Testing;

namespace RimBank.Conformance.Tests;

// Roadmap 1.14: the body, status and payload assertions pass a conforming fake context and fail each kind of
// non-conformance with a message that names the BIAN operation or channel and the JSON path (ADR 0037).
public sealed class FakeContextBodyConformanceTests
{
    private const string Register = "PartyReferenceDataDirectoryEntry/Register";
    private const string RegisterRoute = "/PartyReferenceDataDirectory/Register";
    private const string Created = "PartyReferenceDataDirectoryEntry/Created";
    private const string Adr = "ADR 0024: a test deviation";

    private static readonly JsonSerializerOptions _http = JsonSerializerOptions.Default;
    private static readonly JsonSerializerOptions _wire = new(JsonSerializerOptions.Default) { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static Task<BianServiceDomainSpecification> Party => BianSpecifications.LoadAsync("PartyReferenceDataDirectory");

    [Fact]
    public async Task A_conforming_subset_passes()
    {
        var endpoints = await FakeContext.MapEndpointsAsync(
            new FakeEndpoint("POST", RegisterRoute, typeof(ConformingRegister), Response: typeof(ConformingRegister)),
            new FakeEndpoint("GET", "/PartyReferenceDataDirectory/{id}/Retrieve", Response: typeof(ConformingRegister)));

        endpoints.ShouldHaveBodiesConformingTo(await Party, _http, []);
    }

    [Fact]
    public async Task Endpoints_outside_the_service_domain_or_the_spec_are_ignored()
    {
        var endpoints = await FakeContext.MapEndpointsAsync(
            new FakeEndpoint("POST", "/mcp", typeof(RegisterWithExtraProperty), Status: null),
            new FakeEndpoint("POST", "/PartyReferenceDataDirectory/{id}/Archive", typeof(RegisterWithExtraProperty)));

        endpoints.ShouldHaveBodiesConformingTo(await Party, _http, []);
    }

    [Fact]
    public async Task A_request_property_that_BIAN_does_not_declare_fails()
    {
        var endpoints = await FakeContext.MapEndpointsAsync(new FakeEndpoint("POST", RegisterRoute, typeof(RegisterWithExtraProperty)));

        var failure = Should.Throw<ShouldAssertException>(async () => endpoints.ShouldHaveBodiesConformingTo(await Party, _http, []));

        failure.Message.ShouldContain("failed with 1 problem(s)");
        failure.Message.ShouldContain($"{Register} request RegisterWithExtraProperty: $.Archived: 'Archived' is not a property of the BIAN schema RegisterPartyReferenceDataDirectoryEntry");
        failure.Message.ShouldContain($"({Register} request $.Archived)");
    }

    [Fact]
    public async Task A_nested_response_property_that_BIAN_does_not_declare_fails()
    {
        var endpoints = await FakeContext.MapEndpointsAsync(new FakeEndpoint("POST", RegisterRoute, typeof(ConformingRegister), Response: typeof(RegisterWithExtraNestedProperty)));

        var failure = Should.Throw<ShouldAssertException>(async () => endpoints.ShouldHaveBodiesConformingTo(await Party, _http, []));

        failure.Message.ShouldContain($"{Register} 200 RegisterWithExtraNestedProperty: $.PartyReference.Nickname: 'Nickname' is not a property of the BIAN schema Party");
    }

    [Fact]
    public async Task An_incompatible_JSON_type_fails()
    {
        var endpoints = await FakeContext.MapEndpointsAsync(new FakeEndpoint("POST", RegisterRoute, typeof(RegisterWithWrongType)));

        var failure = Should.Throw<ShouldAssertException>(async () => endpoints.ShouldHaveBodiesConformingTo(await Party, _http, []));

        failure.Message.ShouldContain("$.PartyReference: RimBank writes a JSON string where the BIAN schema Party declares an object.");
    }

    [Fact]
    public async Task A_success_status_that_BIAN_does_not_declare_fails()
    {
        var endpoints = await FakeContext.MapEndpointsAsync(new FakeEndpoint("POST", RegisterRoute, typeof(ConformingRegister), StatusCodes.Status201Created, typeof(ConformingRegister)));

        var failure = Should.Throw<ShouldAssertException>(async () => endpoints.ShouldHaveBodiesConformingTo(await Party, _http, []));

        failure.Message.ShouldContain($"{Register} answers 201 but the BIAN specification declares 200.");
    }

    [Fact]
    public async Task An_endpoint_without_a_declared_success_response_fails()
    {
        var endpoints = await FakeContext.MapEndpointsAsync(new FakeEndpoint("POST", RegisterRoute, typeof(ConformingRegister), Status: null));

        var failure = Should.Throw<ShouldAssertException>(async () => endpoints.ShouldHaveBodiesConformingTo(await Party, _http, []));

        failure.Message.ShouldContain($"{Register} (POST {RegisterRoute}) declares no 2xx response");
    }

    [Fact]
    public async Task A_command_that_reads_no_body_where_BIAN_declares_one_fails()
    {
        var endpoints = await FakeContext.MapEndpointsAsync(new FakeEndpoint("POST", RegisterRoute, Response: typeof(ConformingRegister)));

        var failure = Should.Throw<ShouldAssertException>(async () => endpoints.ShouldHaveBodiesConformingTo(await Party, _http, []));

        failure.Message.ShouldContain("failed with 1 problem(s)");
        failure.Message.ShouldContain($"{Register} (POST {RegisterRoute}) reads no JSON body but the BIAN specification declares one");
    }

    [Fact]
    public async Task A_query_that_reads_a_body_where_BIAN_declares_none_fails()
    {
        var endpoints = await FakeContext.MapEndpointsAsync(
            new FakeEndpoint("GET", "/PartyReferenceDataDirectory/{id}/Retrieve", typeof(ConformingRegister), Response: typeof(ConformingRegister)));

        var failure = Should.Throw<ShouldAssertException>(async () => endpoints.ShouldHaveBodiesConformingTo(await Party, _http, []));

        failure.Message.ShouldContain("PartyReferenceDataDirectoryEntry/Retrieve reads a ConformingRegister body but the BIAN specification declares no request body.");
    }

    [Fact]
    public async Task A_listed_deviation_excuses_exactly_its_path()
    {
        var endpoints = await FakeContext.MapEndpointsAsync(new FakeEndpoint("POST", RegisterRoute, typeof(RegisterWithExtraProperty)));

        endpoints.ShouldHaveBodiesConformingTo(await Party, _http, [new(Register, ConformancePart.Request, "$.Archived", Adr)]);
        Should.Throw<ShouldAssertException>(async () =>
            endpoints.ShouldHaveBodiesConformingTo(await Party, _http, [new(Register, ConformancePart.Response, "$.Archived", Adr)]));
    }

    [Fact]
    public async Task A_listed_deviation_that_no_longer_occurs_fails()
    {
        var endpoints = await FakeContext.MapEndpointsAsync(new FakeEndpoint("POST", RegisterRoute, typeof(ConformingRegister)));

        var failure = Should.Throw<ShouldAssertException>(async () =>
            endpoints.ShouldHaveBodiesConformingTo(await Party, _http, [new(Register, ConformancePart.Request, "$.Archived", Adr)]));

        failure.Message.ShouldContain($"The deviation {Register} request $.Archived no longer occurs; remove it from the list.");
    }

    [Fact]
    public async Task A_listed_deviation_without_an_ADR_fails()
    {
        var endpoints = await FakeContext.MapEndpointsAsync(new FakeEndpoint("POST", RegisterRoute, typeof(RegisterWithExtraProperty)));

        var failure = Should.Throw<ShouldAssertException>(async () =>
            endpoints.ShouldHaveBodiesConformingTo(await Party, _http, [new(Register, ConformancePart.Request, "$.Archived", "we needed it")]));

        failure.Message.ShouldContain($"The deviation {Register} request $.Archived cites no ADR");
    }

    [Fact]
    public async Task A_payload_matching_BIAN_names_without_regard_to_case_passes()
    {
        Type[] published = [typeof(ConformingCreated)];

        published.ShouldHavePayloadsConformingTo(await Party, _wire, []);
    }

    [Fact]
    public async Task A_payload_property_that_BIAN_does_not_declare_fails_unless_listed()
    {
        Type[] published = [typeof(CreatedWithEventId)];

        var failure = Should.Throw<ShouldAssertException>(async () => published.ShouldHavePayloadsConformingTo(await Party, _wire, []));

        failure.Message.ShouldContain($"CreatedWithEventId on {Created}: $.eventId: 'eventId' is not a property of the BIAN schema PartyReferenceDataDirectoryEntry");
        published.ShouldHavePayloadsConformingTo(await Party, _wire, [new(Created, ConformancePart.Payload, "$.eventId", Adr)]);
    }

    [Fact]
    public async Task A_payload_on_an_unknown_channel_or_without_a_channel_fails()
    {
        Type[] published = [typeof(Deleted), typeof(Unchannelled)];

        var failure = Should.Throw<ShouldAssertException>(async () => published.ShouldHavePayloadsConformingTo(await Party, _wire, []));

        failure.Message.ShouldContain("failed with 2 problem(s)");
        failure.Message.ShouldContain("Deleted is published on 'PartyReferenceDataDirectoryEntry/Deleted', which is not a channel of the BIAN AsyncAPI specification.");
        failure.Message.ShouldContain("Unchannelled declares no public const string Channel");
    }
}
