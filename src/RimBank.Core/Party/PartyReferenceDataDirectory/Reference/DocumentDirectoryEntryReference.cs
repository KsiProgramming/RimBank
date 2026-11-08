using System.Text.Json.Serialization;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The reference of the party's documents in a document directory: BIAN
/// <c>DocumentDirectoryEntryInstanceReference.DocumentDirectoryEntryIdentification</c> (roadmap 1.20, ADR 0032). It is
/// trimmed, not blank and at most 35 characters (ISO 20022 Max35Text). It is opaque: BIAN Document Directory is not a
/// RimBank context, so the reference is recorded but never resolved.
/// </summary>
[JsonConverter(typeof(PartyReferenceTextJsonConverter<DocumentDirectoryEntryReference>))]
public sealed record DocumentDirectoryEntryReference : IPartyReferenceText<DocumentDirectoryEntryReference>
{
    /// <summary>ISO 20022 Max35Text.</summary>
    public const int MaxLength = 35;

    private DocumentDirectoryEntryReference(string value) => Value = value;

    /// <inheritdoc />
    public string Value { get; }

    /// <inheritdoc />
    public static Result<DocumentDirectoryEntryReference> TryCreate(string? text) =>
        PartyReferenceText.Read(text, MaxLength, "document directory entry identification")
            .Map(value => new DocumentDirectoryEntryReference(value));

    /// <inheritdoc />
    public override string ToString() => Value;
}
