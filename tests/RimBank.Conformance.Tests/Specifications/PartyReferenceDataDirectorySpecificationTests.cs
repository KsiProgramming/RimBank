using RimBank.Conformance.Testing;

namespace RimBank.Conformance.Specifications.Tests;

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
