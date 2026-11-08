using System.Globalization;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>Reads the ISO 8601 dates and date-times of a Reference Update (roadmap 1.18, ADR 0029).</summary>
internal static class PartyReferenceDates
{
    private static readonly string[] _dateTimeFormats =
    [
        "yyyy-MM-dd'T'HH:mmK",
        "yyyy-MM-dd'T'HH:mm:ssK",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
    ];

    /// <summary>
    /// An ISO 8601 date-time such as <c>2024-01-01T00:00:00Z</c> or <c>2024-01-01T09:30:00+01:00</c>, with its offset as
    /// written. A date-time without an offset is read as UTC.
    /// </summary>
    public static bool TryReadDateTime(string text, out DateTimeOffset value) =>
        DateTimeOffset.TryParseExact(
            text.Trim(),
            _dateTimeFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out value);

    /// <summary>
    /// A point in time kept in UTC: an ISO 8601 date-time (one without an offset is UTC) or a date alone (00:00 UTC that
    /// day). Used by the association periods (roadmap 1.11) and the bank relationship dates (roadmap 1.12).
    /// </summary>
    public static bool TryReadUtcInstant(string text, out DateTimeOffset value)
    {
        if (DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            value = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            return true;
        }

        if (TryReadDateTime(text, out var dateTime))
        {
            value = dateTime.ToUniversalTime();
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// A calendar date: an ISO 8601 date such as <c>1990-05-17</c>, or the date as written in an ISO 8601 date-time, with
    /// no time-zone conversion, because a date of birth has no time of day.
    /// </summary>
    public static bool TryReadDate(string text, out DateOnly value)
    {
        if (DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
        {
            return true;
        }

        if (TryReadDateTime(text, out var dateTime))
        {
            value = DateOnly.FromDateTime(dateTime.DateTime);
            return true;
        }

        value = default;
        return false;
    }
}
