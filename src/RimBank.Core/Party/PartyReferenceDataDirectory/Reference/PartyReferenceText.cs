using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>Reads a Reference text that has only a length rule (an identifier or a name): trimmed, not blank, and not too long.</summary>
internal static class PartyReferenceText
{
    public static Result<string> Read(string? text, int maxLength, string name) =>
        text?.Trim() switch
        {
            null or "" => PartyReferenceErrors.ValueInvalid($"The {name} must not be empty."),
            { Length: var length } when length > maxLength =>
                PartyReferenceErrors.ValueInvalid($"The {name} must be at most {maxLength} characters."),
            var trimmed => trimmed,
        };
}
