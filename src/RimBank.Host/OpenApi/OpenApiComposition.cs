using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using RimBank.Api;
using RimBank.SharedKernel;

namespace RimBank.Host.OpenApi;

/// <summary>
/// The host's OpenAPI document (<c>v1</c>, registered by the service defaults and served in Development only): it
/// describes the HTTP conventions of RimBank.Api that the generator cannot see, such as the <c>Idempotency-Key</c> header
/// of every command endpoint (specs/tech-stack.md §5, ADR 0018).
/// </summary>
public static class OpenApiComposition
{
    /// <summary>The document the service defaults register and map.</summary>
    public const string DocumentName = "v1";

    /// <summary>Adds the RimBank conventions to the <see cref="DocumentName"/> document.</summary>
    /// <param name="services">The host's services.</param>
    /// <returns>The same services.</returns>
    public static IServiceCollection AddRimBankOpenApi(this IServiceCollection services)
    {
        services.Configure<OpenApiOptions>(DocumentName, options => options.AddOperationTransformer<IdempotencyKeyOperationTransformer>());
        return services;
    }

    /// <summary>
    /// Declares the required <c>Idempotency-Key</c> header on every endpoint that carries
    /// <see cref="IdempotencyKeyRequirement"/>, that is every command endpoint (ADR 0018).
    /// </summary>
    internal sealed class IdempotencyKeyOperationTransformer : IOpenApiOperationTransformer
    {
        public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
        {
            if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IdempotencyKeyRequirement>().FirstOrDefault() is { } requirement)
            {
                operation.Parameters ??= [];
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = requirement.HeaderName,
                    In = ParameterLocation.Header,
                    Required = true,
                    Description = "The caller's key for this request. The same key and body return the first answer again; "
                        + "the same key with another body is a 409 (ADR 0018).",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String, MinLength = 1, MaxLength = IdempotencyKey.MaxLength },
                });
            }

            return Task.CompletedTask;
        }
    }
}
