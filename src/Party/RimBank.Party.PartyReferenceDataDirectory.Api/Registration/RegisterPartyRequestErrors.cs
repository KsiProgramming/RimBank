using RimBank.SharedKernel;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Registration;

/// <summary>The validation errors of <see cref="RegisterPartyRequest"/>; their codes are documented in ADR 0024.</summary>
public static class RegisterPartyRequestErrors
{
    /// <summary>The request has no <c>PartyType</c> (400).</summary>
    public static Error PartyTypeRequired { get; } = Error.Validation(
        "party.party-type-required",
        "The party type is required.",
        new Dictionary<string, IReadOnlyList<string>>
        {
            ["PartyType"] = ["The party type is required: Person or Organisation."],
        });
}
