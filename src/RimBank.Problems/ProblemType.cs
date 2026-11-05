using RimBank.SharedKernel;

namespace RimBank.Problems;

/// <summary>The Problem Details <c>type</c> of an <see cref="Error"/>: a stable URN made from its code (ADR 0006, ADR 0015).</summary>
public static class ProblemType
{
    /// <summary>Every RimBank problem type starts with this prefix.</summary>
    public const string Prefix = "urn:rimbank:problem:";

    /// <summary>
    /// <c>urn:rimbank:problem:&lt;context&gt;:&lt;error-name&gt;</c> for the code <c>&lt;context&gt;.&lt;error-name&gt;</c>,
    /// for example <c>urn:rimbank:problem:party:party-not-found</c>.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <returns>The problem type URN.</returns>
    public static string For(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        // The code convention allows exactly one dot, between the context and the error name.
        return Prefix + error.Code.Replace('.', ':');
    }
}
