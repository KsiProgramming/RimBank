using Alba;
using Npgsql;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

/// <summary>A started <c>RimBank.Host</c> on its own database; disposing it stops the host.</summary>
public sealed class PartyApiHost(IAlbaHost host, NpgsqlDataSource administrator) : IAsyncDisposable
{
    /// <summary>The number of entry streams the context's store holds.</summary>
    public const string Streams = "SELECT count(*) FROM party_reference_data_directory.mt_streams";

    /// <summary>The number of command outcomes the idempotency policy stored (ADR 0018).</summary>
    public const string Outcomes = "SELECT count(*) FROM party_reference_data_directory.mt_doc_idempotentoutcome";

    /// <summary>The host, to run Alba scenarios against.</summary>
    public IAlbaHost Alba => host;

    /// <summary>Runs a single-value query as the administrator, to look at what was stored.</summary>
    public async Task<T> ScalarAsync<T>(string sql)
    {
        await using var command = administrator.CreateCommand(sql);
        return (T)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    public async ValueTask DisposeAsync()
    {
        await host.DisposeAsync();
        await administrator.DisposeAsync();
    }
}
