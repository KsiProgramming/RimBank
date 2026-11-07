using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The changes a BIAN <c>Demographics/Update</c> asks for: a partial update (ADR 0024). For each text field,
/// <c>null</c> keeps the current value, <c>""</c> clears it, and any other value is the new, trimmed text. A given
/// <see cref="Employment"/> replaces the whole employment, and an empty one clears it. Adapters build it with
/// <see cref="Create"/>, which checks the ISO 20022 text lengths.
/// </summary>
/// <param name="SocioEconomicClassification">The change of the socio-economic classification.</param>
/// <param name="Employment">The new employment, or <c>null</c> to keep the current one.</param>
/// <param name="EmploymentHistory">The change of the employment history.</param>
/// <param name="EducationHistory">The change of the education history.</param>
/// <param name="ServicingConstraints">The change of the servicing constraints.</param>
public sealed record DemographicsChanges(
    string? SocioEconomicClassification,
    Employment? Employment,
    string? EmploymentHistory,
    string? EducationHistory,
    string? ServicingConstraints)
{
    /// <summary>ISO 20022 Max140Text: the classification and the parts of the employment.</summary>
    public const int ShortTextMaxLength = 140;

    /// <summary>ISO 20022 Max2048Text: the histories and the servicing constraints.</summary>
    public const int LongTextMaxLength = 2048;

    /// <summary>No change at all: an Update that only records that the details were reviewed.</summary>
    public static DemographicsChanges None { get; } = new(null, null, null, null, null);

    /// <summary>
    /// Reads the changes a caller sent: trims every text, so a blank one becomes <c>""</c> (clear), and checks the
    /// lengths. Every adapter (HTTP, MCP) builds the changes here, so they accept and refuse exactly the same values.
    /// </summary>
    /// <param name="socioEconomicClassification">The classification sent, if any.</param>
    /// <param name="employment">
    /// The employment sent, if any. Its parts are read like the other texts, and a missing or blank part is unknown.
    /// </param>
    /// <param name="employmentHistory">The employment history sent, if any.</param>
    /// <param name="educationHistory">The education history sent, if any.</param>
    /// <param name="servicingConstraints">The servicing constraints sent, if any.</param>
    /// <returns>The changes, or <c>party.demographics-invalid</c> naming every field that is too long.</returns>
    public static Result<DemographicsChanges> Create(
        string? socioEconomicClassification,
        Employment? employment,
        string? employmentHistory,
        string? educationHistory,
        string? servicingConstraints)
    {
        var fieldErrors = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var changes = new DemographicsChanges(
            Read(socioEconomicClassification, DemographicsFields.SocioEconomicClassification, ShortTextMaxLength, fieldErrors),
            employment switch
            {
                null => null,
                _ => new Employment(
                    Known(Read(employment.PartyReference, DemographicsFields.EmploymentPartyReference, ShortTextMaxLength, fieldErrors)),
                    Known(Read(employment.InvolvementReference, DemographicsFields.EmploymentInvolvementReference, ShortTextMaxLength, fieldErrors))),
            },
            Read(employmentHistory, DemographicsFields.EmploymentHistory, LongTextMaxLength, fieldErrors),
            Read(educationHistory, DemographicsFields.EducationHistory, LongTextMaxLength, fieldErrors),
            Read(servicingConstraints, DemographicsFields.ServicingConstraints, LongTextMaxLength, fieldErrors));

        return fieldErrors.Count switch
        {
            0 => changes,
            _ => DemographicsErrors.DemographicsInvalid(fieldErrors),
        };
    }

    private static string? Read(string? text, string field, int maxLength, Dictionary<string, IReadOnlyList<string>> fieldErrors)
    {
        var trimmed = text?.Trim();
        if (trimmed is { Length: var length } && length > maxLength)
        {
            fieldErrors[field] = [$"The value must be at most {maxLength} characters."];
        }

        return trimmed;
    }

    private static string? Known(string? text) => string.IsNullOrEmpty(text) ? null : text;
}
