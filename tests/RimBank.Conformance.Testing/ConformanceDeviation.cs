namespace RimBank.Conformance.Testing;

/// <summary>Which body of an operation or channel a <see cref="ConformanceDeviation"/> is about.</summary>
public enum ConformancePart
{
    /// <summary>The JSON request body of an operation.</summary>
    Request,

    /// <summary>A JSON success (2xx) response body of an operation.</summary>
    Response,

    /// <summary>The payload of a published integration event on a channel.</summary>
    Payload,
}

/// <summary>
/// A deliberate difference between a RimBank body and its BIAN schema, decided in an ADR (ADR 0037): a field RimBank
/// must carry that BIAN does not declare, or a field shaped differently. A listed deviation exempts exactly the
/// problem at its JSON path; a deviation that no longer occurs fails, so the list stays true.
/// </summary>
/// <param name="Target">The BIAN operation id (for example <c>PartyReferenceDataDirectoryEntry/Register</c>) or channel address.</param>
/// <param name="Part">The body the deviation is in.</param>
/// <param name="JsonPath">The JSON path of the field, as the conformance failure prints it, for example <c>$.DemographicsId</c>.</param>
/// <param name="Reason">Why, citing the ADR that decided it (<c>ADR 0024: …</c>).</param>
public sealed record ConformanceDeviation(string Target, ConformancePart Part, string JsonPath, string Reason)
{
    /// <inheritdoc />
    public override string ToString() => $"{Target} {Part.ToString().ToLowerInvariant()} {JsonPath}";
}
