using Microsoft.AspNetCore.Http;
using RimBank.SharedKernel;

namespace RimBank.Problems;

/// <summary>The HTTP status of each <see cref="ErrorKind"/>: the table of ADR 0006, in the one place it lives (ADR 0015).</summary>
public static class ErrorStatus
{
    /// <summary>The HTTP status code a caller sees for an error of this kind.</summary>
    /// <param name="kind">The kind of error.</param>
    /// <returns>400, 401, 403, 404, 409, 422, 428 or 500.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a defined <see cref="ErrorKind"/>.</exception>
    public static int For(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => StatusCodes.Status400BadRequest,
        ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.BusinessRule => StatusCodes.Status422UnprocessableEntity,
        ErrorKind.PreconditionRequired => StatusCodes.Status428PreconditionRequired,
        ErrorKind.Unexpected => StatusCodes.Status500InternalServerError,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Every ErrorKind needs an HTTP status in ADR 0006."),
    };
}
