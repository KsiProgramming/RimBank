using System.Text.Json;

namespace RimBank.Conformance.Tests;

// Test-only bodies for the fake context, shaped against the real BIAN Party Reference Data Directory schemas
// (RegisterPartyReferenceDataDirectoryEntry, Party, PartyReferenceDataDirectoryEntry). C# property names are the JSON
// names, because the self-tests write HTTP bodies with JsonSerializerOptions.Default (no naming policy).

/// <summary>
/// A subset of BIAN's Register body: an enum wrapper flattened to a string, a date, a nested Party, and a field accepted
/// as any JSON (a <see cref="JsonElement"/> passthrough).
/// </summary>
internal sealed record ConformingRegister(
    string PartyType,
    DateTimeOffset DirectoryEntryDate,
    ConformingParty? PartyReference = null,
    JsonElement? DirectoryEntryDateType = null);

/// <summary>A subset of BIAN's <c>Party</c>.</summary>
internal sealed record ConformingParty(string Identification);

/// <summary>Register with a property BIAN does not declare.</summary>
internal sealed record RegisterWithExtraProperty(string PartyType, bool Archived);

/// <summary>Register whose nested Party has a property BIAN does not declare.</summary>
internal sealed record RegisterWithExtraNestedProperty(string PartyType, PartyWithNickname PartyReference);

/// <summary>A Party with a property BIAN does not declare.</summary>
internal sealed record PartyWithNickname(string Identification, string Nickname);

/// <summary>Register whose <c>PartyReference</c> is a string where BIAN declares a Party object.</summary>
internal sealed record RegisterWithWrongType(string PartyType, string PartyReference);

/// <summary>A published event whose camelCase wire names match BIAN's PascalCase payload names.</summary>
internal sealed record ConformingCreated(string PartyType, DateTimeOffset DirectoryEntryDate)
{
    public const string Channel = "PartyReferenceDataDirectoryEntry/Created";
}

/// <summary>A published event with a field BIAN's payload does not declare.</summary>
internal sealed record CreatedWithEventId(Guid EventId, string PartyType)
{
    public const string Channel = "PartyReferenceDataDirectoryEntry/Created";
}

/// <summary>A published event on a channel BIAN does not declare.</summary>
internal sealed record Deleted(string PartyType)
{
    public const string Channel = "PartyReferenceDataDirectoryEntry/Deleted";
}

/// <summary>A published event without a channel.</summary>
internal sealed record Unchannelled(string PartyType);
