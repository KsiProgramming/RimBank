namespace RimBank.Host.Modules;

/// <summary>
/// The service domains this host runs, in registration order. The list is explicit (no assembly scanning), so the
/// host's composition is visible in one place and is the same on every start (ADR 0014).
/// </summary>
public static class ServiceDomainModules
{
    /// <summary>
    /// Every module, registered and mapped in this order. It is empty until Stage 1 adds the first context
    /// (<c>new PartyReferenceDataDirectoryModule()</c>).
    /// </summary>
    public static IReadOnlyList<IServiceDomainModule> All { get; } = [];
}
