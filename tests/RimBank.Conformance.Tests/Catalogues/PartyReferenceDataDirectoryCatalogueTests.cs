using System.Text.RegularExpressions;
using RimBank.Conformance.Testing;

namespace RimBank.Conformance.Tests;

// Roadmap 1.1: the ADR 0024 catalogue matches the BIAN v14 ext-DDD specifications of Party Reference Data Directory
// row for row, so the ADR cannot drift from the spec it interprets.
public sealed partial class PartyReferenceDataDirectoryCatalogueTests
{
    [Fact]
    public async Task Every_specified_operation_is_catalogued_once()
    {
        var specification = await BianSpecifications.LoadAsync(PartyReferenceDataDirectoryCatalogue.ServiceDomain);
        var catalogued = PartyReferenceDataDirectoryCatalogue.Operations.Select(row => row.OperationId).ToList();
        var specified = specification.Operations.Select(operation => operation.OperationId).ToList();

        Duplicates(catalogued).ShouldBeEmpty("operations catalogued more than once");
        specified.Except(catalogued, StringComparer.Ordinal).ShouldBeEmpty("specified operations missing from the catalogue");
        catalogued.Except(specified, StringComparer.Ordinal).ShouldBeEmpty("catalogued operations that the specification does not have");
    }

    [Fact]
    public async Task Each_catalogued_kind_matches_the_specification()
    {
        var specification = await BianSpecifications.LoadAsync(PartyReferenceDataDirectoryCatalogue.ServiceDomain);

        var mismatches = PartyReferenceDataDirectoryCatalogue.Operations
            .Select(row => (row, specified: specification.Operation(row.OperationId)))
            .Where(pair => pair.specified is not null && pair.specified.Kind != pair.row.Kind)
            .Select(pair => $"{pair.row.OperationId}: catalogued {pair.row.Kind}, specified {pair.specified!.Kind}");

        mismatches.ShouldBeEmpty("Retrieve operations are queries and every other operation is a command");
    }

    [Fact]
    public async Task Every_specified_channel_is_catalogued_once()
    {
        var specification = await BianSpecifications.LoadAsync(PartyReferenceDataDirectoryCatalogue.ServiceDomain);
        var catalogued = PartyReferenceDataDirectoryCatalogue.Channels.Select(row => row.Address).ToList();
        var specified = specification.Channels.Select(channel => channel.Address).ToList();

        Duplicates(catalogued).ShouldBeEmpty("channels catalogued more than once");
        specified.Except(catalogued, StringComparer.Ordinal).ShouldBeEmpty("specified channels missing from the catalogue");
        catalogued.Except(specified, StringComparer.Ordinal).ShouldBeEmpty("catalogued channels that the specification does not have");
    }

    [Fact]
    public void Phases_are_stage_one_or_deferred()
    {
        var phases = PartyReferenceDataDirectoryCatalogue.Operations.Select(row => (Name: row.OperationId, row.Phase))
            .Concat(PartyReferenceDataDirectoryCatalogue.Channels.Select(row => (Name: row.Address, row.Phase)));

        phases.Where(row => !StageOnePhase().IsMatch(row.Phase) && row.Phase != PartyReferenceDataDirectoryCatalogue.Deferred)
            .Select(row => $"{row.Name}: {row.Phase}")
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task Each_published_channel_has_its_event_and_each_deferred_channel_has_none()
    {
        var specification = await BianSpecifications.LoadAsync(PartyReferenceDataDirectoryCatalogue.ServiceDomain);
        var published = PartyReferenceDataDirectoryCatalogue.Channels.Where(row => row.PublishedEvent is not null).ToList();

        PartyReferenceDataDirectoryCatalogue.Channels
            .Where(row => (row.Phase == PartyReferenceDataDirectoryCatalogue.Deferred) == (row.PublishedEvent is not null))
            .Select(row => row.Address)
            .ShouldBeEmpty("a channel has an event exactly when it is not deferred");
        published.Select(row => $"{row.Address} <- {row.PublishedEvent!.GetField("Channel")?.GetRawConstantValue()}")
            .ShouldBe(published.Select(row => $"{row.Address} <- {row.Address}"), "each event declares the channel it is catalogued on");
        published.Select(row => row.Address).ShouldBeChannelsOf(specification);
    }

    private static List<string> Duplicates(IEnumerable<string> values) =>
        values.GroupBy(value => value, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key).ToList();

    [GeneratedRegex(@"^1\.[1-9][0-9]*$")]
    private static partial Regex StageOnePhase();
}
