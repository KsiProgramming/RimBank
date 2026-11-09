using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Shouldly;

namespace RimBank.Conformance.Testing;

/// <summary>The body, status and payload conformance assertions (roadmap 1.14, ADR 0037).</summary>
public static partial class BianConformance
{
    private const string ChannelField = "Channel";

    /// <summary>
    /// Asserts that the bodies and success statuses of a context's endpoints conform to its BIAN OpenAPI specification:
    /// <list type="number">
    /// <item>every endpoint under <c>/&lt;ServiceDomain&gt;/</c> declares at least one 2xx response, and each one is a 2xx the specification declares for that operation;</item>
    /// <item>an endpoint reads a JSON body exactly when the specification declares one;</item>
    /// <item>every property of its request and 2xx response bodies, as <paramref name="json"/> writes them, exists in the BIAN schema at the same JSON path, with the exact name and a compatible JSON type;</item>
    /// <item>every <paramref name="deviations"/> row about a request or response is still needed, and cites an ADR.</item>
    /// </list>
    /// Routes that are not an operation of the specification are left to <see cref="ShouldConformTo"/>.
    /// </summary>
    /// <param name="endpoints">The endpoints the application maps (<see cref="ImplementedEndpoint.From(Microsoft.AspNetCore.Routing.EndpointDataSource)"/>).</param>
    /// <param name="specification">The service domain's specification.</param>
    /// <param name="json">The serializer options the application writes HTTP bodies with.</param>
    /// <param name="deviations">The context's ADR-decided deviations.</param>
    /// <exception cref="ShouldAssertException">At least one problem was found; the message lists all of them.</exception>
    public static void ShouldHaveBodiesConformingTo(
        this IEnumerable<ImplementedEndpoint> endpoints,
        BianServiceDomainSpecification specification,
        JsonSerializerOptions json,
        IEnumerable<ConformanceDeviation> deviations)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(deviations);

        var checkedOperations = new List<string>();
        var found = new List<(ConformanceDeviation At, string Message)>();
        foreach (var endpoint in endpoints.Where(endpoint => InServiceDomain(specification, endpoint.Route)).Distinct())
        {
            if (specification.Operations.FirstOrDefault(operation =>
                    operation.Method == endpoint.Route.Method && operation.Path.Equals(endpoint.Route.Route)) is not { } operation)
            {
                continue;
            }

            checkedOperations.Add(operation.OperationId);
            found.AddRange(BodyProblems(operation, endpoint, json));
        }

        var listed = deviations.Where(deviation => deviation.Part is ConformancePart.Request or ConformancePart.Response).ToList();
        Report(specification, Unexcused(found, listed, checkedOperations));
    }

    /// <summary>
    /// Asserts that the payloads of a context's published integration events conform to its BIAN AsyncAPI specification:
    /// each event declares a <c>Channel</c> constant that is a channel of the specification, and every property it puts on
    /// the wire, as <paramref name="wire"/> writes it, exists in the channel's payload schema at the same JSON path,
    /// compared without regard to case (events are camelCase on the wire, ADR 0036), with a compatible JSON type. Every
    /// <paramref name="deviations"/> row about a payload must still be needed and cite an ADR.
    /// </summary>
    /// <param name="publishedEvents">The event types the context publishes.</param>
    /// <param name="specification">The service domain's specification.</param>
    /// <param name="wire">The serializer options the bus writes messages with.</param>
    /// <param name="deviations">The context's ADR-decided deviations.</param>
    /// <exception cref="ShouldAssertException">At least one problem was found; the message lists all of them.</exception>
    public static void ShouldHavePayloadsConformingTo(
        this IEnumerable<Type> publishedEvents,
        BianServiceDomainSpecification specification,
        JsonSerializerOptions wire,
        IEnumerable<ConformanceDeviation> deviations)
    {
        ArgumentNullException.ThrowIfNull(publishedEvents);
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentNullException.ThrowIfNull(wire);
        ArgumentNullException.ThrowIfNull(deviations);

        var checkedChannels = new List<string>();
        var found = new List<(ConformanceDeviation At, string Message)>();
        foreach (var published in publishedEvents.Distinct())
        {
            if (published.GetField(ChannelField, BindingFlags.Public | BindingFlags.Static)?.GetRawConstantValue() is not string address)
            {
                found.Add((new(published.Name, ConformancePart.Payload, "$", string.Empty), $"{published.Name} declares no public const string {ChannelField} (ADR 0036)."));
                continue;
            }

            var at = new ConformanceDeviation(address, ConformancePart.Payload, "$", string.Empty);
            checkedChannels.Add(address);
            if (specification.Channels.FirstOrDefault(channel => channel.Address == address) is not { } channel)
            {
                found.Add((at, $"{published.Name} is published on '{address}', which is not a channel of the BIAN AsyncAPI specification."));
            }
            else if (channel.Payload is not { } payload)
            {
                found.Add((at, $"{published.Name} is published on '{address}', whose BIAN message declares no payload schema."));
            }
            else
            {
                found.AddRange(SchemaSubset.Compare(published, wire, payload, StringComparison.OrdinalIgnoreCase)
                    .Select(problem => (at with { JsonPath = problem.JsonPath }, $"{published.Name} on {address}: {problem.JsonPath}: {problem.Message}")));
            }
        }

        var listed = deviations.Where(deviation => deviation.Part == ConformancePart.Payload).ToList();
        Report(specification, Unexcused(found, listed, checkedChannels));
    }

    private static bool InServiceDomain(BianServiceDomainSpecification specification, ImplementedRoute route) =>
        string.Equals(route.Route.FirstSegment, specification.ServiceDomain, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<(ConformanceDeviation At, string Message)> BodyProblems(BianOperation operation, ImplementedEndpoint endpoint, JsonSerializerOptions json)
    {
        var id = operation.OperationId;
        var request = new ConformanceDeviation(id, ConformancePart.Request, "$", string.Empty);
        var response = request with { Part = ConformancePart.Response };

        if (endpoint.RequestType is { } requestType)
        {
            if (operation.RequestBody is not { } requestBody)
            {
                yield return (request, $"{id} reads a {requestType.Name} body but the BIAN specification declares no request body.");
            }
            else
            {
                foreach (var problem in SchemaSubset.Compare(requestType, json, requestBody, StringComparison.Ordinal))
                {
                    yield return (request with { JsonPath = problem.JsonPath }, $"{id} request {requestType.Name}: {problem.JsonPath}: {problem.Message}");
                }
            }
        }

        else if (operation.RequestBody is not null)
        {
            yield return (request, $"{id} ({endpoint}) reads no JSON body but the BIAN specification declares one; bind the body as a parameter or declare it with .Accepts<T>().");
        }

        var successes = endpoint.SuccessResponses.ToList();
        if (successes.Count == 0)
        {
            yield return (response, $"{id} ({endpoint}) declares no 2xx response; declare its success body with .Produces<T>(status).");
        }

        foreach (var success in successes)
        {
            if (!operation.SuccessResponses.TryGetValue(success.StatusCode, out var schema))
            {
                yield return (response, $"{id} answers {success.StatusCode} but the BIAN specification declares {Statuses(operation)}.");
            }
            else if (success.Type is { } responseType && schema is null)
            {
                yield return (response, $"{id} answers {success.StatusCode} with a {responseType.Name} body but the BIAN specification declares no body for it.");
            }
            else if (success.Type is { } bodyType && schema is not null)
            {
                foreach (var problem in SchemaSubset.Compare(bodyType, json, schema, StringComparison.Ordinal))
                {
                    yield return (response with { JsonPath = problem.JsonPath }, $"{id} {success.StatusCode} {bodyType.Name}: {problem.JsonPath}: {problem.Message}");
                }
            }
        }
    }

    private static string Statuses(BianOperation operation) =>
        operation.SuccessResponses.Count == 0 ? "no 2xx response" : string.Join(" or ", operation.SuccessResponses.Keys.Order());

    // Drops the problems a listed deviation excuses, then adds a problem for each listed deviation that excused nothing
    // on a checked target, or that cites no ADR.
    private static List<string> Unexcused(
        List<(ConformanceDeviation At, string Message)> found,
        List<ConformanceDeviation> listed,
        List<string> checkedTargets)
    {
        var excused = listed.ToHashSet(DeviationKey.Instance);
        List<string> problems =
        [
            .. found.Where(problem => !excused.Contains(problem.At))
                .Select(problem => problem.Message + $" If this is deliberate, list it as a deviation ({problem.At}) citing the ADR that decides it."),
        ];

        var occurred = found.Select(problem => problem.At).ToHashSet(DeviationKey.Instance);
        problems.AddRange(listed
            .Where(deviation => checkedTargets.Contains(deviation.Target, StringComparer.Ordinal) && !occurred.Contains(deviation))
            .Select(deviation => $"The deviation {deviation} no longer occurs; remove it from the list."));
        problems.AddRange(listed
            .Where(deviation => !AdrCitation().IsMatch(deviation.Reason))
            .Select(deviation => $"The deviation {deviation} cites no ADR; its reason must name the ADR that decides it (\"ADR 0024: …\")."));
        return problems;
    }

    [GeneratedRegex(@"\bADR \d{4}\b")]
    private static partial Regex AdrCitation();

    private sealed class DeviationKey : IEqualityComparer<ConformanceDeviation>
    {
        public static readonly DeviationKey Instance = new();

        public bool Equals(ConformanceDeviation? x, ConformanceDeviation? y) =>
            x is not null && y is not null && x.Target == y.Target && x.Part == y.Part && x.JsonPath == y.JsonPath;

        public int GetHashCode(ConformanceDeviation obj) => HashCode.Combine(obj.Target, obj.Part, obj.JsonPath);
    }
}
