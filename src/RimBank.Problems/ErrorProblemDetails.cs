using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using RimBank.SharedKernel;

namespace RimBank.Problems;

/// <summary>Turns an <see cref="Error"/> into RFC 9457 Problem Details (ADR 0015).</summary>
public static class ErrorProblemDetails
{
    /// <summary>
    /// The Problem Details of <paramref name="error"/>: <c>type</c> from <see cref="ProblemType"/>, <c>status</c> from
    /// <see cref="ErrorStatus"/>, <c>title</c> the status reason phrase, <c>detail</c> the error message, and the
    /// <c>errorCode</c> extension. A validation error with field errors becomes an
    /// <see cref="HttpValidationProblemDetails"/> whose <c>errors</c> lists them.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <param name="instance">The URI reference of this occurrence, usually the request path; <c>null</c> to let the host fill it in.</param>
    /// <returns>The Problem Details, without <c>traceId</c> (see <see cref="ProblemDetailsEnrichment"/>).</returns>
    public static ProblemDetails Create(Error error, string? instance = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        var status = ErrorStatus.For(error.Kind);
        var problem = error.Kind == ErrorKind.Validation && error.FieldErrors.Count > 0
            ? new HttpValidationProblemDetails(error.FieldErrors.ToDictionary(field => field.Key, field => field.Value.ToArray(), StringComparer.Ordinal))
            : new ProblemDetails();

        problem.Type = ProblemType.For(error);
        problem.Title = ReasonPhrases.GetReasonPhrase(status);
        problem.Status = status;
        problem.Detail = error.Message;
        problem.Instance = instance;
        problem.Extensions[ProblemExtensions.ErrorCode] = error.Code;

        return problem;
    }

    /// <summary>
    /// The endpoint result for <paramref name="error"/>: an <c>application/problem+json</c> response with the
    /// Problem Details of <see cref="Create"/>. This is what a <c>.Api</c> endpoint returns for a failed <c>Result</c>.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <returns>The HTTP result.</returns>
    public static IResult ToHttpResult(this Error error) => TypedResults.Problem(Create(error));
}
