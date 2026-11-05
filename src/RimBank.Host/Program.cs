using RimBank.Host.Modules;

namespace RimBank.Host;

/// <summary>
/// The entry point of <c>RimBank.Host</c>. It is a named class in the <c>RimBank.Host</c> namespace (not top-level
/// statements) so the namespace rule (ADR 0011) holds and tests can use <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public sealed class Program
{
    private Program()
    {
    }

    /// <summary>Builds and runs the host.</summary>
    /// <param name="args">The command-line arguments.</param>
    public static void Main(string[] args) =>
        HostComposition.Build(WebApplication.CreateBuilder(args), ServiceDomainModules.All).Run();
}
