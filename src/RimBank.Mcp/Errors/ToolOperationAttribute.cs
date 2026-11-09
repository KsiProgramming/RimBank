namespace RimBank.Mcp;

/// <summary>
/// The BIAN operation a tool performs, and so the authorisation policy a caller must meet to see and call it: the policy
/// named by <see cref="OperationId"/>, the same one the operation's HTTP endpoint requires (ADR 0038, ADR 0039).
/// </summary>
/// <remarks>
/// It replaces <c>[Authorize(Policy = …)]</c> on tools. With the SDK 2.3.1, <c>[Authorize]</c> needs the SDK's
/// authorization filters, which always run first and refuse a call with a JSON-RPC error, so the caller could not get the
/// <c>shared.forbidden</c> Problem Details HTTP returns (mission principle 8). <see cref="ToolAuthorizationFilter"/>
/// applies this attribute instead.
/// </remarks>
/// <param name="operationId">The BIAN operation id, for example <c>PartyReferenceDataDirectoryEntry/Register</c>.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ToolOperationAttribute(string operationId) : Attribute
{
    /// <summary>The BIAN operation id, which is also the name of its authorisation policy.</summary>
    public string OperationId { get; } = operationId;
}
