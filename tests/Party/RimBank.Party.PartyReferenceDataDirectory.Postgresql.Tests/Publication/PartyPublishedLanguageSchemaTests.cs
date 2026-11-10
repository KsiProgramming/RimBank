using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using RimBank.Postgresql;
using Wolverine;
using Wolverine.Util;
using Published = RimBank.Party.PartyReferenceDataDirectory.PublishedLanguage;

namespace RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests;

// Roadmap 1.13: the schema contract of the published language (ADR 0036). Each published event's JSON schema on the
// wire, with its channel and message identity, is pinned by an approved file next to this test. A field removed,
// renamed or retyped, or a changed identity, fails here; an added field fails too, until the approved file is
// updated on purpose in the same pull request. On a mismatch the test writes <identity>.received.json beside it.
public sealed class PartyPublishedLanguageSchemaTests
{
    // Wolverine's default System.Text.Json settings for messages (camelCase, case-insensitive reads, strict numbers).
    // The wire_names test below proves they still match what Wolverine writes.
    private static readonly JsonSerializerOptions _wire = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonSerializerOptions _approvedFile = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly DateTimeOffset _at = new(2026, 11, 8, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid _eventId = Guid.CreateVersion7(_at);
    private static readonly Guid _entryId = Guid.CreateVersion7(_at);

    public static TheoryData<Type> PublishedEvents { get; } =
        [.. typeof(Published.PartyTypes).Assembly.GetExportedTypes()
            .Where(type => type.GetField(PublishedLanguageMessageNaming.MessageIdentityField) is not null)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)];

    [Fact]
    public void The_published_language_holds_the_two_control_record_events()
    {
        PublishedEvents.Select(row => row.Data).ShouldBe(
            [typeof(Published.PartyReferenceDataDirectoryEntryCreated), typeof(Published.PartyReferenceDataDirectoryEntryUpdated)],
            ignoreOrder: true);
    }

    [Theory]
    [MemberData(nameof(PublishedEvents))]
    public void The_schema_on_the_wire_matches_the_approved_contract(Type published)
    {
        var identity = PublishedLanguageMessageNaming.DeclaredIdentity(published).ShouldNotBeNull();
        var contract = new JsonObject
        {
            ["channel"] = (string?)published.GetField("Channel")?.GetRawConstantValue(),
            ["messageIdentity"] = identity,
            ["schema"] = JsonSchemaExporter.GetJsonSchemaAsNode(
                _wire,
                published,
                new JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true }),
        };
        var received = contract.ToJsonString(_approvedFile).ReplaceLineEndings("\n") + "\n";
        var approvedPath = ApprovedPath(identity);
        var receivedPath = Path.ChangeExtension(approvedPath, null).Replace(".approved", ".received", StringComparison.Ordinal) + ".json";

        var approved = File.Exists(approvedPath) ? File.ReadAllText(approvedPath).ReplaceLineEndings("\n") : null;
        if (approved == received)
        {
            File.Delete(receivedPath);
            return;
        }

        File.WriteAllText(receivedPath, received);
        approved.ShouldBe(
            received,
            $"The published contract of {identity} changed. A breaking change needs a new versioned event (ADR 0036). "
            + $"If the change only adds a field, review {receivedPath} and copy it over {approvedPath}.");
    }

    [Theory]
    [MemberData(nameof(PublishedEvents))]
    public void Every_published_event_goes_on_the_wire_under_its_declared_identity(Type published)
    {
        PublishedLanguageMessageNaming.Register();

        var identity = PublishedLanguageMessageNaming.DeclaredIdentity(published).ShouldNotBeNull();

        identity.ShouldMatch(@"^[a-z]+(-[a-z]+)*\.[a-z]+\.v[1-9][0-9]*$");
        published.ToMessageTypeName().ShouldBe(identity);
    }

    [Fact]
    public void The_wire_names_are_the_ones_Wolverine_writes()
    {
        var created = Created();

        var written = JsonNode.Parse(new WolverineOptions().DefaultSerializer.WriteMessage(created))!.AsObject();

        written.Select(property => property.Key).ShouldBe(
            JsonSerializer.SerializeToNode(created, _wire)!.AsObject().Select(property => property.Key));
    }

    [Fact]
    public void A_published_event_survives_the_wire_round_trip()
    {
        var serializer = new WolverineOptions().DefaultSerializer;
        object[] events = [Created(), Updated()];

        events.Select(published => serializer.ReadFromData(published.GetType(), new Envelope { Data = serializer.WriteMessage(published) }))
            .ShouldBe(events);
    }

    [Fact]
    public void A_consumer_reading_v1_tolerates_a_field_added_later()
    {
        var serializer = new WolverineOptions().DefaultSerializer;
        var payload = JsonSerializer.SerializeToNode(Created(), _wire)!.AsObject();
        payload["addedLater"] = "a value an older consumer does not know";

        var read = serializer.ReadFromData(
            typeof(Published.PartyReferenceDataDirectoryEntryCreated),
            new Envelope { Data = JsonSerializer.SerializeToUtf8Bytes(payload) });

        read.ShouldBe(Created());
    }

    private static Published.PartyReferenceDataDirectoryEntryCreated Created() =>
        new(_eventId, _at, _entryId, 1, Published.PartyTypes.Person, Published.DirectoryEntryDateTypes.OpenDate, _at);

    private static Published.PartyReferenceDataDirectoryEntryUpdated Updated() =>
        new(_eventId, _at, _entryId, 2, Published.PartyTypes.Organisation, Published.DirectoryEntryDateTypes.RefreshDate, _at);

    // The project directory is embedded at build time (see the .csproj): [CallerFilePath] is mapped to /_/ on CI.
    private static string ApprovedPath(string identity) =>
        Path.Combine(ProjectDirectory(), "Publication", "Approved", identity + ".approved.json");

    private static string ProjectDirectory() =>
        typeof(PartyPublishedLanguageSchemaTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "ProjectDirectory")
            .Value
            .ShouldNotBeNull();
}
