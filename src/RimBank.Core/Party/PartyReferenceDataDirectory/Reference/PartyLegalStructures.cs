using System.Collections.Frozen;
using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The BIAN spelling of each <see cref="PartyLegalStructure"/> (roadmap 1.20, ADR 0032), so every adapter (HTTP, MCP)
/// reads and answers the same names.
/// </summary>
public static class PartyLegalStructures
{
    private static readonly FrozenDictionary<PartyLegalStructure, string> _bianNames =
        new Dictionary<PartyLegalStructure, string>
        {
            [PartyLegalStructure.Individual] = "Individual",
            [PartyLegalStructure.CommunityInterestCompany] = "CommunityInterestCompany",
            [PartyLegalStructure.CharitableIncorporatedOrganisation] = "CharitableIncorporatedOrganisation",
            [PartyLegalStructure.CoOperative] = "Co-Operative",
            [PartyLegalStructure.Charity] = "Charity",
            [PartyLegalStructure.GeneralPartnership] = "GeneralPartnership",
            [PartyLegalStructure.LimitedLiabilityPartnership] = "LimitedLiabilityPartnership",
            [PartyLegalStructure.ScottishLimitedPartnership] = "ScottishLimitedPartnership",
            [PartyLegalStructure.LimitedPartnership] = "LimitedPartnership",
            [PartyLegalStructure.PrivateLimitedCompany] = "PrivateLimitedCompany",
            [PartyLegalStructure.PublicLimitedCompany] = "PublicLimitedCompany",
            [PartyLegalStructure.SoleTrader] = "Sole(SoleTrader)",
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, PartyLegalStructure> _byName =
        _bianNames.ToFrozenDictionary(pair => Comparable(pair.Value), pair => pair.Key, StringComparer.Ordinal);

    /// <summary>The BIAN spelling of every legal structure, in enum order.</summary>
    public static IReadOnlyList<string> BianNames { get; } = [.. _bianNames.OrderBy(pair => pair.Key).Select(pair => pair.Value)];

    /// <summary>The BIAN spelling of a legal structure, for example <c>Co-Operative</c>.</summary>
    /// <param name="structure">The legal structure.</param>
    /// <returns>Its BIAN name.</returns>
    public static string ToBianName(this PartyLegalStructure structure) => _bianNames[structure];

    /// <summary>
    /// Reads a legal structure a caller sent: matched without regard to case, spaces or hyphens, so <c>co-operative</c>,
    /// <c>CoOperative</c> and <c>private limited company</c> are read.
    /// </summary>
    /// <param name="text">The text sent, not blank.</param>
    /// <returns>The legal structure, or <c>party.reference-invalid</c>.</returns>
    public static Result<PartyLegalStructure> Read(string text) =>
        _byName.TryGetValue(Comparable(text), out var structure)
            ? structure
            : PartyReferenceErrors.ValueInvalid($"The legal structure must be one of: {string.Join(", ", BianNames)}.");

    private static string Comparable(string name) =>
        new([.. name.Where(character => !char.IsWhiteSpace(character) && character != '-').Select(char.ToUpperInvariant)]);
}
