using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using RimBank.SharedKernel;

namespace RimBank.Api;

/// <summary>
/// The <c>Idempotency-Key</c> header of a command endpoint (specs/tech-stack.md §5, ADR 0018), bound as an endpoint
/// parameter: <c>(RegisterPartyRequest request, IdempotencyKeyHeader idempotency, IMessageBus bus) => …</c>.
/// </summary>
/// <remarks>
/// <para>
/// Binding never fails on its own: a missing or invalid header is kept as a failed <see cref="Key"/>. Put
/// <see cref="IdempotencyKeyEndpoints.RequireIdempotencyKey{TBuilder}"/> on the endpoint (or its group), and it answers
/// 400 Problem Details (<c>shared.idempotency-key-missing</c> or <c>shared.idempotency-key-invalid</c>) before the
/// endpoint runs, so the endpoint can read <see cref="Value"/> safely.
/// </para>
/// <para>
/// A key reused with a different payload, or still in use, comes back from the command as a <c>Conflict</c>
/// <see cref="Error"/>, which <c>error.ToHttpResult()</c> turns into 409 Problem Details.
/// </para>
/// </remarks>
public sealed class IdempotencyKeyHeader : IEndpointParameterMetadataProvider
{
    /// <summary>The header name (IETF draft <c>Idempotency-Key</c> HTTP header field).</summary>
    public const string HeaderName = "Idempotency-Key";

    private IdempotencyKeyHeader(Result<IdempotencyKey> key) => Key = key;

    /// <summary>The key, or why the header is not one.</summary>
    public Result<IdempotencyKey> Key { get; }

    /// <summary>The key. The endpoint must require it with <see cref="IdempotencyKeyEndpoints.RequireIdempotencyKey{TBuilder}"/>.</summary>
    /// <exception cref="InvalidOperationException">The header is missing or invalid (the endpoint did not require it).</exception>
    public IdempotencyKey Value => Key.Value;

    /// <summary>Reads the header from the request. Called by ASP.NET Core parameter binding.</summary>
    /// <param name="context">The request.</param>
    /// <returns>The header, valid or not.</returns>
    public static ValueTask<IdempotencyKeyHeader?> BindAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ValueTask.FromResult<IdempotencyKeyHeader?>(new IdempotencyKeyHeader(Read(context.Request)));
    }

    /// <summary>Describes the required header in the endpoint's metadata, for the OpenAPI document.</summary>
    /// <param name="parameter">The endpoint parameter.</param>
    /// <param name="builder">The endpoint builder.</param>
    public static void PopulateMetadata(ParameterInfo parameter, EndpointBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Metadata.Add(IdempotencyKeyRequirement.Instance);
    }

    internal static Result<IdempotencyKey> Read(HttpRequest request)
    {
        var values = request.Headers[HeaderName];

        // One key per request: several headers, or one with several values, is not a key.
        return values.Count switch
        {
            0 => IdempotencyKey.Missing,
            1 => IdempotencyKey.TryCreate(values[0]),
            _ => IdempotencyKey.Invalid,
        };
    }
}

/// <summary>Endpoint metadata: this endpoint changes state and requires an <c>Idempotency-Key</c> header (ADR 0018).</summary>
/// <param name="HeaderName">The header name.</param>
public sealed record IdempotencyKeyRequirement(string HeaderName)
{
    /// <summary>The requirement every command endpoint carries.</summary>
    public static IdempotencyKeyRequirement Instance { get; } = new(IdempotencyKeyHeader.HeaderName);
}
