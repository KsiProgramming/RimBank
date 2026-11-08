using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>The BIAN spelling of each <see cref="PoliticalExposureType"/> (roadmap 1.20, ADR 0032).</summary>
public static class PoliticalExposureTypes
{
    /// <summary>The BIAN spelling of a political exposure type, for example <c>Politicalexposuredomestic</c>.</summary>
    /// <param name="type">The type.</param>
    /// <returns>Its BIAN name.</returns>
    public static string ToBianName(this PoliticalExposureType type) =>
        type switch
        {
            PoliticalExposureType.Foreign => "Politicalexposureforeign",
            PoliticalExposureType.Domestic => "Politicalexposuredomestic",
            PoliticalExposureType.NoExposure => "Nopoliticalexposure",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Not a political exposure type."),
        };

    /// <summary>Whether the type declares an exposure, foreign or domestic.</summary>
    /// <param name="type">The type.</param>
    /// <returns><c>true</c> for <see cref="PoliticalExposureType.Foreign"/> and <see cref="PoliticalExposureType.Domestic"/>.</returns>
    public static bool IsExposed(this PoliticalExposureType type) =>
        type is PoliticalExposureType.Foreign or PoliticalExposureType.Domestic;

    /// <summary>Reads a political exposure type a caller sent: trimmed, BIAN names matched without regard to case.</summary>
    /// <param name="text">The text sent, not blank.</param>
    /// <returns>The type, or <c>party.reference-invalid</c>.</returns>
    public static Result<PoliticalExposureType> Read(string text) =>
        text.Trim().ToUpperInvariant() switch
        {
            "POLITICALEXPOSUREFOREIGN" => PoliticalExposureType.Foreign,
            "POLITICALEXPOSUREDOMESTIC" => PoliticalExposureType.Domestic,
            "NOPOLITICALEXPOSURE" => PoliticalExposureType.NoExposure,
            _ => PartyReferenceErrors.ValueInvalid(
                "The political exposure type must be Politicalexposureforeign, Politicalexposuredomestic or Nopoliticalexposure."),
        };
}
