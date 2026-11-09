using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RimBank.Mcp;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// <c>party_reference_data_directory_retrieve_reference</c>: BIAN <c>Reference/Retrieve</c>, a query (ADR 0024). It
/// dispatches <see cref="RetrievePartyReference"/>, as <c>GET …/Reference/{referenceid}/Retrieve</c> does, and answers
/// only from the entry's inline view (tech-stack §6, §8, ADR 0039).
/// </summary>
[McpServerToolType]
public sealed class RetrievePartyReferenceTool
{
    /// <summary>The tool's name (ADR 0021).</summary>
    public const string Name = "party_reference_data_directory_retrieve_reference";

    /// <summary>The BIAN operation id, which names the tool's authorisation policy (ADR 0038).</summary>
    public const string OperationId = "Reference/Retrieve";

    /// <summary>Retrieves the party's Reference, as the HTTP endpoint does: the same query and errors.</summary>
    /// <param name="partyReferenceDataDirectoryEntryId">The entry's id.</param>
    /// <param name="referenceId">The entry's ReferenceId.</param>
    /// <param name="bus">The host's message bus.</param>
    /// <param name="cancellationToken">The call's cancellation.</param>
    /// <returns>The Reference with the entry's <c>version</c>, or the Problem Details of the error.</returns>
    [McpServerTool(Name = Name, Title = "Retrieve a party's reference", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [ToolOperation(OperationId)]
    [Description(
        "BIAN Party Reference Data Directory: retrieve details about the customer's reference entry. Returns the tax "
        + "identification and national registration numbers in GovernmentIssuedIdentityReference, the LEI in "
        + "PartyLegalEntityReference.Identification, PartyFullLegalName, PartyNameSalutation, DateofBirth (YYYY-MM-DD), "
        + "Nationality and PartyCountryOfResidence (ISO 3166 Code), ResidencyStatus, the ResidentialAddress, PartyAddress "
        + "and CorporateAddress postal addresses, the eMailAddress, CellPhoneNumber and SocialNetworkContacts contact "
        + "points, the declared legal standing and documents, and the corporate and officer references, with the entry's "
        + "version (send it as expectedVersion to change the entry). Unknown values are left out.")]
    public static async Task<CallToolResult> RetrieveAsync(
        [Description("The PartyReferenceDataDirectoryEntryId that Register returned.")] string partyReferenceDataDirectoryEntryId,
        [Description("The entry's ReferenceId, from Register or Retrieve.")] string referenceId,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bus);

        var ids = ReferenceIds.Read(partyReferenceDataDirectoryEntryId, referenceId);
        if (ids.IsFailure)
        {
            return ToolProblem.Create(ids.Error);
        }

        var view = await bus.InvokeAsync<Result<PartyReferenceDataDirectoryEntryView>>(
            new RetrievePartyReference(ids.Value.EntryId, ids.Value.PartyReferenceId),
            cancellationToken);
        return view.ToToolResult(found => ToolResults.Structured(PartyReferenceResult.From(found)));
    }
}
