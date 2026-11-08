using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// Reads the ISO 3166 <c>Code</c> of a BIAN <c>Country</c> object a caller sent (roadmap 1.18, ADR 0030): trimmed and
/// upper-cased, then an officially assigned alpha-2 code. Used by <c>Nationality</c>, <c>PartyCountryOfResidence</c> and
/// the country of every postal address (1.19).
/// </summary>
internal static class PartyReferenceCountry
{
    public const string Message = "The country must be an ISO 3166-1 alpha-2 code, for example FR.";

    public static Result<CountryCode> Read(string code) =>
        CountryCode.TryCreate(code.Trim().ToUpperInvariant()) switch
        {
            { IsSuccess: true } known => known.Value,
            _ => PartyReferenceErrors.ValueInvalid(Message),
        };
}
