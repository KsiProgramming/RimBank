using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests;

/// <summary>A started host with the Party store on its own database; disposing it stops the host.</summary>
public sealed class PartyHost(IHost host, NpgsqlDataSource administrator) : IAsyncDisposable
{
    /// <summary>The context's own store.</summary>
    public IPartyReferenceDataDirectoryStore Store => host.Services.GetRequiredService<IPartyReferenceDataDirectoryStore>();

    /// <summary>The context's read port, as the host registers it for query handlers.</summary>
    public IPartyDirectoryEnquiry Enquiry => host.Services.GetRequiredService<IPartyDirectoryEnquiry>();

    /// <summary>The host's message bus, as an <c>.Api</c> or <c>.Mcp</c> adapter uses it.</summary>
    public IMessageBus Bus => host.Services.GetRequiredService<IMessageBus>();

    /// <summary>The running host itself, for Wolverine's message tracking.</summary>
    public IHost Application => host;

    /// <summary>Runs a single-value query as the administrator, to look at what was stored.</summary>
    public async Task<T> ScalarAsync<T>(string sql)
    {
        await using var command = administrator.CreateCommand(sql);
        return (T)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    public async ValueTask DisposeAsync()
    {
        await host.StopAsync(CancellationToken.None);
        host.Dispose();
        await administrator.DisposeAsync();
    }
}
