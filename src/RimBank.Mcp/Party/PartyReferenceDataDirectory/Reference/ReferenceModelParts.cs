using System.Text.Json;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>Names the parts of a Reference request object that were sent with a value but are not recorded (ADR 0029).</summary>
internal static class ReferenceModelParts
{
    /// <summary>The names of the properties sent with a value (not <c>null</c>).</summary>
    public static IEnumerable<string> Present(Dictionary<string, JsonElement>? properties) =>
        (properties ?? []).Where(property => property.Value.ValueKind != JsonValueKind.Null).Select(property => property.Key);

    /// <summary>The BIAN paths of the properties sent with a value, under <paramref name="field"/>.</summary>
    public static IEnumerable<string> Present(string field, Dictionary<string, JsonElement>? properties) =>
        Present(properties).Select(name => $"{field}.{name}");
}
