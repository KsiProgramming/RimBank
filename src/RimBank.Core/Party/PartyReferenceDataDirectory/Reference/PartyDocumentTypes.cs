using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>Reads a <see cref="PartyDocumentType"/> a caller sent (roadmap 1.20, ADR 0032).</summary>
public static class PartyDocumentTypes
{
    /// <summary>Reads a document type: trimmed, BIAN names matched without regard to case.</summary>
    /// <param name="text">The text sent, not blank.</param>
    /// <returns>The type, or <c>party.reference-invalid</c>.</returns>
    public static Result<PartyDocumentType> Read(string text) =>
        text.Trim().ToUpperInvariant() switch
        {
            "WORKDOCUMENT" => PartyDocumentType.WorkDocument,
            "CERTIFICATIONDOCUMENT" => PartyDocumentType.CertificationDocument,
            "PERSONALDOCUMENT" => PartyDocumentType.PersonalDocument,
            _ => PartyReferenceErrors.ValueInvalid(
                "The party document type must be WorkDocument, CertificationDocument or PersonalDocument."),
        };
}
