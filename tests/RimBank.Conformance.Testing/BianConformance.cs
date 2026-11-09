using Shouldly;

namespace RimBank.Conformance.Testing;

/// <summary>
/// The BIAN conformance assertions (roadmap 0.14, ADR 0020). A context's <c>.Api.Tests</c> loads its specification
/// with <see cref="BianSpecifications.LoadAsync"/>, reads its routes with <c>ImplementedRoute.From</c>, and
/// declares the BIAN operations and channels it implements. Every problem found is reported in one failure.
/// </summary>
public static partial class BianConformance
{
    private const string Get = "GET";

    /// <summary>
    /// Asserts that the routes of a context conform to its BIAN OpenAPI specification:
    /// <list type="number">
    /// <item>the specification itself follows the command/query rule (only <c>Retrieve</c> is <c>GET</c>);</item>
    /// <item>every route under <c>/&lt;ServiceDomain&gt;/</c> is an operation of the specification, with the same method;</item>
    /// <item>a <c>Retrieve</c> operation is mapped as <c>GET</c> (a query), and no other operation is mapped as <c>GET</c>;</item>
    /// <item>every declared operation exists in the specification and is mapped.</item>
    /// </list>
    /// Routes outside the service domain (health probes, <c>/mcp</c>, other contexts) are ignored.
    /// </summary>
    /// <param name="specification">The service domain's specification.</param>
    /// <param name="routes">The routes the application maps.</param>
    /// <param name="declaredOperationIds">The BIAN operation ids the context implements, for example <c>PartyReferenceDataDirectoryEntry/Register</c>.</param>
    /// <exception cref="ShouldAssertException">At least one problem was found; the message lists all of them.</exception>
    public static void ShouldConformTo(
        this IEnumerable<ImplementedRoute> routes,
        BianServiceDomainSpecification specification,
        IEnumerable<string> declaredOperationIds)
    {
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentNullException.ThrowIfNull(declaredOperationIds);

        var inScope = routes
            .Where(route => string.Equals(route.Route.FirstSegment, specification.ServiceDomain, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();

        List<string> problems =
        [
            .. SpecificationProblems(specification),
            .. inScope.Select(route => RouteProblem(specification, route)).OfType<string>(),
            .. declaredOperationIds.Distinct(StringComparer.Ordinal).Select(id => DeclarationProblem(specification, inScope, id)).OfType<string>(),
        ];

        Report(specification, problems);
    }

    /// <summary>Asserts that every channel a context publishes exists in its BIAN AsyncAPI specification.</summary>
    /// <param name="publishedAddresses">The channel addresses the context publishes to, for example <c>PartyReferenceDataDirectoryEntry/Created</c>.</param>
    /// <param name="specification">The service domain's specification.</param>
    /// <exception cref="ShouldAssertException">At least one address is not in the specification; the message lists all of them.</exception>
    public static void ShouldBeChannelsOf(this IEnumerable<string> publishedAddresses, BianServiceDomainSpecification specification)
    {
        ArgumentNullException.ThrowIfNull(publishedAddresses);
        ArgumentNullException.ThrowIfNull(specification);

        var known = specification.Channels.Select(channel => channel.Address).ToHashSet(StringComparer.Ordinal);
        List<string> problems =
        [
            .. publishedAddresses
                .Distinct(StringComparer.Ordinal)
                .Where(address => !known.Contains(address))
                .Select(address => $"The channel '{address}' is not in the BIAN AsyncAPI specification. Its channels are: {string.Join(", ", known.Order(StringComparer.Ordinal))}."),
        ];

        Report(specification, problems);
    }

    private static IEnumerable<string> SpecificationProblems(BianServiceDomainSpecification specification) =>
        specification.Operations
            .Where(operation => operation.Kind == BianOperationKind.Query != (operation.Method == Get))
            .Select(operation => $"The BIAN specification declares {operation} as a {operation.Kind.ToString().ToUpperInvariant()} "
                + "but only Retrieve operations are GET queries (mission principle 5, tech-stack §5). This BIAN interpretation needs an ADR.");

    private static string? RouteProblem(BianServiceDomainSpecification specification, ImplementedRoute route)
    {
        var candidates = specification.Operations.Where(operation => operation.Path.Equals(route.Route)).ToList();
        if (candidates.Count == 0)
        {
            return $"{route} is not an operation of the BIAN specification of {specification.ServiceDomain}.";
        }

        if (candidates.Any(operation => operation.Method == route.Method))
        {
            return null;
        }

        var operation = candidates[0];
        return (operation.Kind, route.Method) switch
        {
            (BianOperationKind.Query, _) =>
                $"{operation.OperationId} is a query (Retrieve) but is mapped as {route}; a Retrieve operation is a GET endpoint that dispatches a query (command/query rule, tech-stack §5).",
            (BianOperationKind.Command, Get) =>
                $"{operation.OperationId} is a command but is mapped as {route}; only Retrieve operations are GET, every other operation dispatches a command with {operation.Method} (command/query rule, tech-stack §5).",
            _ =>
                $"{operation.OperationId} is mapped as {route} but the BIAN specification declares {operation.Method} {operation.Path}.",
        };
    }

    private static string? DeclarationProblem(BianServiceDomainSpecification specification, IReadOnlyList<ImplementedRoute> routes, string operationId)
    {
        if (specification.Operation(operationId) is not { } operation)
        {
            return $"The declared operation '{operationId}' is not an operation of the BIAN specification of {specification.ServiceDomain}.";
        }

        return routes.Any(route => route.Method == operation.Method && operation.Path.Equals(route.Route))
            ? null
            : $"The declared operation {operationId} is not mapped: no endpoint maps {operation.Method} {operation.Path}.";
    }

    private static void Report(BianServiceDomainSpecification specification, List<string> problems)
    {
        if (problems.Count > 0)
        {
            throw new ShouldAssertException(
                $"BIAN conformance of {specification.ServiceDomain} failed with {problems.Count} problem(s):{Environment.NewLine}"
                + string.Join(Environment.NewLine, problems.Select(problem => "- " + problem)));
        }
    }
}
