using Microsoft.AspNetCore.Authorization;

namespace RimBank.Host;

/// <summary>Whether a BIAN operation is a command (write side) or a query (read side, the <c>Retrieve</c> operations).</summary>
public enum OperationKind
{
    /// <summary>A command: every BIAN operation except <c>Retrieve</c> (specs/tech-stack.md §8).</summary>
    Command,

    /// <summary>A query: a BIAN <c>Retrieve</c> operation, served from a read model.</summary>
    Query,
}

/// <summary>The authorisation policy of one BIAN operation: its name is the operation id, and it requires one scope (ADR 0038).</summary>
/// <param name="OperationId">The BIAN operation id, for example <c>PartyReferenceDataDirectoryEntry/Register</c>; also the policy name.</param>
/// <param name="Kind">Whether the operation is a command or a query.</param>
/// <param name="Scope">The OAuth scope the caller's token must carry.</param>
public sealed record OperationPolicy(string OperationId, OperationKind Kind, string Scope);

/// <summary>
/// Every operation policy the modules registered, in registration order (ADR 0038). The MCP protected-resource metadata
/// lists its scopes, and tests use it to check that every BIAN endpoint carries its policy.
/// </summary>
public sealed class OperationPolicyCatalog
{
    private readonly List<OperationPolicy> _policies = [];

    /// <summary>The registered policies, in registration order.</summary>
    public IReadOnlyList<OperationPolicy> Policies => _policies;

    /// <summary>The distinct scopes the registered policies require, in registration order.</summary>
    public IReadOnlyList<string> Scopes => _policies.Select(policy => policy.Scope).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>
    /// The policies <paramref name="modules"/> register, without registering them in any host: to list a host's
    /// operations and scopes, for example in tests.
    /// </summary>
    /// <param name="modules">The modules, normally <see cref="ServiceDomainModules.All"/>.</param>
    /// <returns>The catalog of their policies.</returns>
    public static OperationPolicyCatalog Of(IEnumerable<IServiceDomainModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        var catalog = new OperationPolicyCatalog();
        var policies = new OperationPolicies(new AuthorizationBuilder(new ServiceCollection()), catalog);
        foreach (var module in modules)
        {
            module.AddOperationPolicies(policies);
        }

        return catalog;
    }

    /// <summary>The policy of <paramref name="operationId"/>, or <see langword="null"/> when no module registered it.</summary>
    /// <param name="operationId">The BIAN operation id.</param>
    /// <returns>The policy, if any.</returns>
    public OperationPolicy? Find(string operationId) =>
        _policies.FirstOrDefault(policy => string.Equals(policy.OperationId, operationId, StringComparison.Ordinal));

    internal void Add(OperationPolicy policy)
    {
        if (Find(policy.OperationId) is not null)
        {
            throw new InvalidOperationException(
                $"Each BIAN operation has exactly one authorisation policy, but '{policy.OperationId}' is registered more than once (ADR 0038).");
        }

        _policies.Add(policy);
    }
}

/// <summary>
/// What a module uses to register the authorisation policy of each of its BIAN operations (ADR 0038): a command
/// requires the context's write scope, a query its read scope. The policy is named by the operation id, so an endpoint
/// (<c>RequireOperation</c>) and, from roadmap 1.16, an MCP tool refer to it by that id.
/// </summary>
public sealed class OperationPolicies
{
    private readonly AuthorizationBuilder _authorization;
    private readonly OperationPolicyCatalog _catalog;

    internal OperationPolicies(AuthorizationBuilder authorization, OperationPolicyCatalog catalog)
    {
        _authorization = authorization;
        _catalog = catalog;
    }

    /// <summary>Registers the policy of a command: an authenticated caller whose token carries <paramref name="scope"/>.</summary>
    /// <param name="operationId">The BIAN operation id, the endpoint's name.</param>
    /// <param name="scope">The context's write scope.</param>
    /// <returns>The same registration, to chain the next operation.</returns>
    /// <exception cref="InvalidOperationException">The operation already has a policy.</exception>
    public OperationPolicies AddCommand(string operationId, string scope) => Add(operationId, OperationKind.Command, scope);

    /// <summary>Registers the policy of a query: an authenticated caller whose token carries <paramref name="scope"/>.</summary>
    /// <param name="operationId">The BIAN <c>Retrieve</c> operation id, the endpoint's name.</param>
    /// <param name="scope">The context's read scope.</param>
    /// <returns>The same registration, to chain the next operation.</returns>
    /// <exception cref="InvalidOperationException">The operation already has a policy.</exception>
    public OperationPolicies AddQuery(string operationId, string scope) => Add(operationId, OperationKind.Query, scope);

    private OperationPolicies Add(string operationId, OperationKind kind, string scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        if (scope.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException($"A scope is one token without spaces, but '{scope}' has some.", nameof(scope));
        }

        _catalog.Add(new OperationPolicy(operationId, kind, scope));
        _authorization.AddPolicy(operationId, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new OperationScopeRequirement(scope)));
        return this;
    }
}
