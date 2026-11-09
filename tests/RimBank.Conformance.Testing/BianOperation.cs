namespace RimBank.Conformance.Testing;

/// <summary>Whether a BIAN operation changes state (a command) or only reads it (a query).</summary>
public enum BianOperationKind
{
    /// <summary>Every operation other than <c>Retrieve</c>: Register, Initiate, Update, Control, Execute, Exchange, Request, …</summary>
    Command,

    /// <summary>A <c>Retrieve</c> operation (mission principle 5, tech-stack §5).</summary>
    Query,
}

/// <summary>One operation of a BIAN ext-DDD OpenAPI specification.</summary>
/// <param name="OperationId">The BIAN operation id, for example <c>PartyReferenceDataDirectoryEntry/Register</c> or <c>Reference/Retrieve</c>.</param>
/// <param name="Method">The HTTP method the specification declares, upper case (<c>GET</c>, <c>POST</c>, <c>PUT</c>).</param>
/// <param name="Path">The path template the specification declares.</param>
public sealed record BianOperation(string OperationId, string Method, RouteTemplate Path)
{
    /// <summary>The BIAN action: the last segment of the operation id (<c>Register</c>, <c>Retrieve</c>, <c>Update</c>, …).</summary>
    public string Action => OperationId[(OperationId.LastIndexOf('/') + 1)..];

    /// <summary><see cref="BianOperationKind.Query"/> for a <c>Retrieve</c> operation, otherwise <see cref="BianOperationKind.Command"/>.</summary>
    public BianOperationKind Kind => Action == "Retrieve" ? BianOperationKind.Query : BianOperationKind.Command;

    /// <summary>The schema of its JSON request body, or <see langword="null"/> when it has none (ADR 0037).</summary>
    public BianSchema? RequestBody { get; init; }

    /// <summary>
    /// Its success (2xx) responses: status code to JSON body schema, <see langword="null"/> for a response without a body.
    /// Where BIAN wires a 2xx to an error response, the operation's own response component is used (ADR 0037).
    /// </summary>
    public IReadOnlyDictionary<int, BianSchema?> SuccessResponses { get; init; } = new Dictionary<int, BianSchema?>();

    /// <inheritdoc />
    public override string ToString() => $"{OperationId} ({Method} {Path})";
}

/// <summary>One channel of a BIAN ext-DDD AsyncAPI specification, for example <c>PartyReferenceDataDirectoryEntry/Created</c>.</summary>
/// <param name="Address">The channel address.</param>
public sealed record BianChannel(string Address)
{
    /// <summary>The schema of the payload of its message, or <see langword="null"/> when it declares none (ADR 0037).</summary>
    public BianSchema? Payload { get; init; }
}
