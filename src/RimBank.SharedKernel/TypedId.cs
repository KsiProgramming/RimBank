namespace RimBank.SharedKernel;

/// <summary>
/// The shared logic behind every <see cref="ITypedId{TSelf}"/>: creation, validation, parsing and formatting.
/// </summary>
public static class TypedId
{
    /// <summary>The error code for a value that is not a valid identifier (ADR 0006, ADR 0008).</summary>
    public const string InvalidIdentifierCode = "shared.invalid-identifier";
}
