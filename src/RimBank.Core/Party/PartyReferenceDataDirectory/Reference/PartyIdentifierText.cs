using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>Reads the text of an identifier that has only a length rule: trimmed, not blank, and not too long.</summary>
internal static class PartyIdentifierText
{
    public static Result<string> Read(string? text, int maxLength, string name) =>
        text?.Trim() switch
        {
            null or "" => PartyReferenceErrors.IdentifierInvalid($"The {name} must not be empty."),
            { Length: var length } when length > maxLength =>
                PartyReferenceErrors.IdentifierInvalid($"The {name} must be at most {maxLength} characters."),
            var trimmed => trimmed,
        };
}
