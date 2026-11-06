using System.Collections.Concurrent;

namespace RimBank.Conformance.Testing;

/// <summary>
/// Locates and loads the BIAN v14 ext-DDD specifications in <c>doc-0/release14.0.0/apis-iso20022_ext-ddd/</c>
/// (ADR 0002, ADR 0020). The folder is found by walking up from the test output to the repository root, the folder
/// that holds <c>RimBank.slnx</c>. Each service domain is read once per test run.
/// </summary>
public static class BianSpecifications
{
    /// <summary>The specifications folder, relative to the repository root.</summary>
    public const string RelativePath = "doc-0/release14.0.0/apis-iso20022_ext-ddd";

    private const string SolutionFile = "RimBank.slnx";

    private static readonly Lazy<DirectoryInfo> _directory = new(FindDirectory);
    private static readonly ConcurrentDictionary<string, Lazy<Task<BianServiceDomainSpecification>>> _loaded = new(StringComparer.Ordinal);

    /// <summary>The specifications folder.</summary>
    public static DirectoryInfo Directory => _directory.Value;

    /// <summary>The service domains that have an OpenAPI specification, in name order.</summary>
    public static IReadOnlyList<string> ServiceDomains =>
    [
        .. OpenApiFolder.EnumerateFiles("*.yaml")
            .Select(file => Path.GetFileNameWithoutExtension(file.Name))
            .Order(StringComparer.Ordinal),
    ];

    private static DirectoryInfo OpenApiFolder => new(Path.Combine(Directory.FullName, "oas3", "yamls"));

    private static DirectoryInfo AsyncApiFolder => new(Path.Combine(Directory.FullName, "asyncapi-3.x", "yamls"));

    /// <summary>Loads one service domain's OpenAPI and AsyncAPI specifications.</summary>
    /// <param name="serviceDomain">The BIAN service domain name in PascalCase, for example <c>PartyReferenceDataDirectory</c>.</param>
    /// <returns>Its operations and channels. A service domain without an AsyncAPI specification has no channels.</returns>
    /// <exception cref="ArgumentException">There is no OpenAPI specification for <paramref name="serviceDomain"/>.</exception>
    public static Task<BianServiceDomainSpecification> LoadAsync(string serviceDomain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceDomain);
        var openApi = new FileInfo(Path.Combine(OpenApiFolder.FullName, serviceDomain + ".yaml"));
        if (!openApi.Exists || !ServiceDomains.Contains(serviceDomain, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"There is no BIAN specification for the service domain '{serviceDomain}' in {RelativePath}. "
                + $"Available: {string.Join(", ", ServiceDomains)}.",
                nameof(serviceDomain));
        }

        return _loaded.GetOrAdd(serviceDomain, name => new Lazy<Task<BianServiceDomainSpecification>>(() => ReadAsync(name, openApi))).Value;
    }

    private static async Task<BianServiceDomainSpecification> ReadAsync(string serviceDomain, FileInfo openApi)
    {
        var operations = await OpenApiSpecificationReader.ReadAsync(openApi, CancellationToken.None);
        var asyncApi = new FileInfo(Path.Combine(AsyncApiFolder.FullName, serviceDomain + ".yaml"));
        var channels = asyncApi.Exists ? await AsyncApiSpecificationReader.ReadAsync(asyncApi, CancellationToken.None) : [];
        return new BianServiceDomainSpecification(serviceDomain, operations, channels);
    }

    private static DirectoryInfo FindDirectory()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, SolutionFile)))
            {
                var specifications = new DirectoryInfo(Path.Combine(folder.FullName, RelativePath));
                return specifications.Exists
                    ? specifications
                    : throw new DirectoryNotFoundException($"The repository root '{folder.FullName}' has no {RelativePath} folder.");
            }
        }

        throw new DirectoryNotFoundException($"No folder above '{AppContext.BaseDirectory}' holds {SolutionFile}, so the BIAN specifications cannot be found.");
    }
}
