using System.Text.Json;
using Npgsql;
using RimBank.Postgresql.Database.Tests;
using RimBank.SharedKernel;

namespace RimBank.Postgresql.Serialization.Tests;

// Roadmap 1.4: every store writes typed ids as plain GUID strings (ADR 0008) and enums as their names, in its events,
// its documents and the acknowledgements it remembers for idempotency (ADR 0016, Serialization).
public sealed class StoredJsonTests(PostgresqlServer server)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void A_typed_id_is_a_plain_guid_string_and_an_enum_is_its_name()
    {
        var stamp = new FakeStamp(Guid.CreateVersion7(), FakeStampId.New(TimeProvider.System), FakeStampKind.Second);

        var json = JsonSerializer.Serialize(stamp, StoredJson.Options);

        json.ShouldContain($"\"Stamp\":\"{stamp.Stamp.Value}\"");
        json.ShouldContain("\"Kind\":\"Second\"");
        JsonSerializer.Deserialize<FakeStamp>(json, StoredJson.Options).ShouldBe(stamp);
    }

    [Fact]
    public async Task A_store_keeps_typed_ids_as_guid_strings_and_enums_as_names()
    {
        await using var host = await RunningHost.StartAsync(server);
        var stamp = new FakeStamp(Guid.CreateVersion7(), FakeStampId.New(TimeProvider.System), FakeStampKind.Second);
        await using (var session = host.Registry.LightweightSession())
        {
            session.Store(stamp);
            await session.SaveChangesAsync(Cancellation);
        }

        await using (var query = host.Registry.QuerySession())
        {
            (await query.LoadAsync<FakeStamp>(stamp.Id, Cancellation)).ShouldBe(stamp);
        }

        var json = await StoredDataAsync(host, $"SELECT data::text FROM fake_registry.mt_doc_fakestamp WHERE id = '{stamp.Id}'");
        json.ShouldContain($"\"Stamp\": \"{stamp.Stamp.Value}\"");
        json.ShouldContain("\"Kind\": \"Second\"");
    }

    private async Task<string> StoredDataAsync(RunningHost host, string sql)
    {
        await using var dataSource = NpgsqlDataSource.Create(server.AdministratorConnectionString(host.Database));
        await using var command = dataSource.CreateCommand(sql);
        return (string)(await command.ExecuteScalarAsync(Cancellation))!;
    }
}

public enum FakeStampKind
{
    First,
    Second,
}

public sealed record FakeStamp(Guid Id, FakeStampId Stamp, FakeStampKind Kind);

public readonly record struct FakeStampId : ITypedId<FakeStampId>
{
    private FakeStampId(Guid value) => Value = value;

    public Guid Value { get; }

    public static FakeStampId New(TimeProvider time) => new(TypedId.NewGuid(time));

    public static FakeStampId From(Guid value) => new(TypedId.EnsureValid(value));

    public static Result<FakeStampId> TryCreate(Guid value) => TypedId.TryCreate<FakeStampId>(value);

    public static Result<FakeStampId> TryCreate(string? text) => TypedId.TryCreate<FakeStampId>(text);

    public override string ToString() => TypedId.Format(Value);

    public static bool operator <(FakeStampId left, FakeStampId right) => TypedId.Compare(left, right) < 0;

    public static bool operator <=(FakeStampId left, FakeStampId right) => TypedId.Compare(left, right) <= 0;

    public static bool operator >(FakeStampId left, FakeStampId right) => TypedId.Compare(left, right) > 0;

    public static bool operator >=(FakeStampId left, FakeStampId right) => TypedId.Compare(left, right) >= 0;
}
