namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The kind of government-issued document: BIAN <c>PartyDocument.PartyDocumentType</c> (<c>Partydocumenttypevalues</c>;
/// roadmap 1.20, ADR 0032). Stored by name, which is also the BIAN spelling; <see cref="PartyDocumentTypes"/> reads it.
/// </summary>
public enum PartyDocumentType
{
    /// <summary>BIAN <c>WorkDocument</c>, for example a work permit.</summary>
    WorkDocument = 1,

    /// <summary>BIAN <c>CertificationDocument</c>, for example a certificate of incorporation.</summary>
    CertificationDocument = 2,

    /// <summary>BIAN <c>PersonalDocument</c>, for example a passport or an identity card.</summary>
    PersonalDocument = 3,
}
