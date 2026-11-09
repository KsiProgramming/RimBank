using RimBank.Conformance.Testing;

namespace RimBank.Conformance.Tests;

// Roadmap 0.14: the loaders read the real BIAN v14 ext-DDD specifications of Party Reference Data Directory
// (doc-0/release14.0.0/apis-iso20022_ext-ddd/, ADR 0002, ADR 0020).
public sealed class PartyReferenceDataDirectorySpecificationTests
{
    private const string ServiceDomain = "PartyReferenceDataDirectory";

    [Fact]
    public void The_specifications_folder_is_found_from_the_test_output()
    {
        BianSpecifications.Directory.Exists.ShouldBeTrue();
        BianSpecifications.ServiceDomains.ShouldContain(ServiceDomain);
    }

    [Fact]
    public async Task Register_is_a_POST_command()
    {
        var specification = await BianSpecifications.LoadAsync(ServiceDomain);

        var register = specification.Operation("PartyReferenceDataDirectoryEntry/Register").ShouldNotBeNull();
        register.Method.ShouldBe("POST");
        register.Path.Text.ShouldBe("/PartyReferenceDataDirectory/Register");
        register.Action.ShouldBe("Register");
        register.Kind.ShouldBe(BianOperationKind.Command);
    }

    [Fact]
    public async Task Retrieve_is_a_GET_query()
    {
        var specification = await BianSpecifications.LoadAsync(ServiceDomain);

        var retrieve = specification.Operation("PartyReferenceDataDirectoryEntry/Retrieve").ShouldNotBeNull();
        retrieve.Method.ShouldBe("GET");
        retrieve.Path.Text.ShouldBe("/PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Retrieve");
        retrieve.Kind.ShouldBe(BianOperationKind.Query);
    }

    [Fact]
    public async Task Update_is_a_PUT_command()
    {
        var specification = await BianSpecifications.LoadAsync(ServiceDomain);

        var update = specification.Operation("PartyReferenceDataDirectoryEntry/Update").ShouldNotBeNull();
        update.Method.ShouldBe("PUT");
        update.Kind.ShouldBe(BianOperationKind.Command);
    }

    [Theory]
    [InlineData("Reference/Retrieve", "/PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Reference/{referenceid}/Retrieve")]
    [InlineData("Associations/Retrieve", "/PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Associations/{associationsid}/Retrieve")]
    public async Task Behavior_qualifier_retrieves_are_GET_queries(string operationId, string path)
    {
        var specification = await BianSpecifications.LoadAsync(ServiceDomain);

        var retrieve = specification.Operation(operationId).ShouldNotBeNull();
        retrieve.Method.ShouldBe("GET");
        retrieve.Path.Text.ShouldBe(path);
        retrieve.Kind.ShouldBe(BianOperationKind.Query);
    }

    [Fact]
    public async Task The_specification_follows_the_command_query_rule()
    {
        var specification = await BianSpecifications.LoadAsync(ServiceDomain);

        specification.Operations.ShouldNotBeEmpty();
        specification.Operations.ShouldAllBe(operation => (operation.Kind == BianOperationKind.Query) == (operation.Method == "GET"));
    }

    [Fact]
    public async Task The_AsyncAPI_specification_has_a_created_and_an_updated_channel_per_record()
    {
        var specification = await BianSpecifications.LoadAsync(ServiceDomain);

        specification.Channels.Select(channel => channel.Address).ShouldBe(
            [
                "PartyReferenceDataDirectoryEntry/Created",
                "PartyReferenceDataDirectoryEntry/Updated",
                "Demographics/Created",
                "Demographics/Updated",
                "Associations/Created",
                "Associations/Updated",
                "BankRelations/Created",
                "BankRelations/Updated",
                "Reference/Created",
                "Reference/Updated",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Register_reads_its_request_body_with_the_JSON_media_type_BIAN_writes_with_a_colon()
    {
        var specification = await BianSpecifications.LoadAsync(ServiceDomain);

        var body = specification.Operation("PartyReferenceDataDirectoryEntry/Register").ShouldNotBeNull().RequestBody.ShouldNotBeNull();
        body.Name.ShouldBe("RegisterPartyReferenceDataDirectoryEntry");
        body.Type.ShouldBe("object");
        body.PropertyNames.ShouldBe(["PartyReference", "DirectoryEntryDateType", "DirectoryEntryDate", "PartyType"]);
        body.Property("PartyType", StringComparison.Ordinal).ShouldNotBeNull().IsEnumWrapper.ShouldBeTrue();
        body.Property("DirectoryEntryDate", StringComparison.Ordinal).ShouldNotBeNull().Type.ShouldBe("string");
        body.Property("PartyReference", StringComparison.Ordinal).ShouldNotBeNull().Name.ShouldBe("Party");
    }

    [Theory]
    [InlineData("PartyReferenceDataDirectoryEntry/Register", "RegisterPartyReferenceDataDirectoryEntry")]
    [InlineData("PartyReferenceDataDirectoryEntry/Update", "UpdatePartyReferenceDataDirectoryEntry")]
    public async Task A_200_that_BIAN_wires_to_BadRequest_uses_the_operations_own_response(string operationId, string schema)
    {
        var specification = await BianSpecifications.LoadAsync(ServiceDomain);

        var success = specification.Operation(operationId).ShouldNotBeNull().SuccessResponses;
        success.Keys.ShouldBe([200]);
        success[200].ShouldNotBeNull().Name.ShouldBe(schema);
    }

    [Fact]
    public async Task A_query_reads_its_200_response_and_has_no_request_body()
    {
        var specification = await BianSpecifications.LoadAsync(ServiceDomain);

        var retrieve = specification.Operation("Reference/Retrieve").ShouldNotBeNull();
        retrieve.RequestBody.ShouldBeNull();
        retrieve.SuccessResponses[200].ShouldNotBeNull().PropertyNames.ShouldContain("PartyFullLegalName");
    }

    [Fact]
    public async Task A_channel_reads_its_message_payload()
    {
        var specification = await BianSpecifications.LoadAsync(ServiceDomain);

        var payload = specification.Channels.Single(channel => channel.Address == "PartyReferenceDataDirectoryEntry/Created").Payload.ShouldNotBeNull();
        payload.Name.ShouldBe("PartyReferenceDataDirectoryEntry");
        payload.Property("partyType", StringComparison.OrdinalIgnoreCase).ShouldNotBeNull().Type.ShouldBe("string");
        specification.Channels.ShouldAllBe(channel => channel.Payload != null);
    }

    [Fact]
    public async Task A_service_domain_is_read_once()
    {
        var first = await BianSpecifications.LoadAsync(ServiceDomain);
        var second = await BianSpecifications.LoadAsync(ServiceDomain);

        second.ShouldBeSameAs(first);
    }

    [Fact]
    public void An_unknown_service_domain_fails_and_lists_the_available_ones()
    {
        var failure = Should.Throw<ArgumentException>(() => BianSpecifications.LoadAsync("CurrentAcount"));

        failure.Message.ShouldContain("'CurrentAcount'");
        failure.Message.ShouldContain(ServiceDomain);
    }
}
