using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// When an association holds: BIAN <c>AssociationValidFromToDate</c>, the <c>FromDateTime</c> and <c>ToDateTime</c> of an
/// ISO 20022 <c>DateTimePeriod</c> (roadmap 1.11, ADR 0033). At least one bound is known, both are UTC, and
/// <see cref="From"/> is not after <see cref="To"/>. It is never compared with today: an association can be recorded
/// after it ended or before it starts, and a known <see cref="To"/> is how an association is ended.
/// </summary>
/// <param name="From">When the association starts, if known.</param>
/// <param name="To">When the association ends, if known.</param>
public sealed record AssociationValidityPeriod(DateTimeOffset? From, DateTimeOffset? To)
{
    /// <summary>
    /// Reads the period a caller sent. Each bound is an ISO 8601 date-time (one without an offset is UTC) or a date
    /// (00:00 UTC that day), and is kept in UTC.
    /// </summary>
    /// <param name="input">The bounds sent; at least one is not blank.</param>
    /// <returns>
    /// The period, or a validation error whose field errors name <see cref="PartyAssociationFields.FromDateTime"/> or
    /// <see cref="PartyAssociationFields.ToDateTime"/> (relative to the period).
    /// </returns>
    public static Result<AssociationValidityPeriod> TryCreate(AssociationValidityPeriodInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var fieldErrors = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var from = ReadBound(input.FromDateTime, PartyAssociationFields.FromDateTime, fieldErrors);
        var to = ReadBound(input.ToDateTime, PartyAssociationFields.ToDateTime, fieldErrors);

        if (fieldErrors.Count == 0 && from is null && to is null)
        {
            fieldErrors[PartyAssociationFields.FromDateTime] = ["The validity period needs a FromDateTime, a ToDateTime or both."];
        }

        if (fieldErrors.Count == 0 && from > to)
        {
            fieldErrors[PartyAssociationFields.ToDateTime] = ["The end of the validity period must not be before its start."];
        }

        return fieldErrors.Count switch
        {
            0 => new AssociationValidityPeriod(from, to),
            _ => Error.Validation(PartyAssociationErrors.AssociationInvalidCode, "The validity period is not valid.", fieldErrors),
        };
    }

    private static DateTimeOffset? ReadBound(string? text, string field, Dictionary<string, IReadOnlyList<string>> fieldErrors)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (PartyReferenceDates.TryReadUtcInstant(text, out var instant))
        {
            return instant;
        }

        fieldErrors[field] = ["The value must be an ISO 8601 date-time, for example 2024-01-01T00:00:00Z, or date."];
        return null;
    }
}

/// <summary>The validity period a caller sent, as raw text (roadmap 1.11).</summary>
/// <param name="FromDateTime">BIAN <c>FromDateTime</c>, if sent.</param>
/// <param name="ToDateTime">BIAN <c>ToDateTime</c>, if sent.</param>
public sealed record AssociationValidityPeriodInput(string? FromDateTime, string? ToDateTime)
{
    /// <summary>Whether both bounds are missing or blank: such a period clears the recorded one.</summary>
    public bool IsBlank => string.IsNullOrWhiteSpace(FromDateTime) && string.IsNullOrWhiteSpace(ToDateTime);
}
