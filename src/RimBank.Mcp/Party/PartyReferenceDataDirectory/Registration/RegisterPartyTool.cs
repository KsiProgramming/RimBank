using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RimBank.Mcp;
using RimBank.SharedKernel;
using Wolverine;

namespace RimBank.Party.PartyReferenceDataDirectory.Mcp;

/// <summary>
/// <c>party_reference_data_directory_register</c>: BIAN <c>PartyReferenceDataDirectoryEntry/Register</c>, a command
/// (ADR 0024). It dispatches <see cref="RegisterParty"/>, as <c>POST /PartyReferenceDataDirectory/Register</c> does, and
/// answers from its acknowledgement, never from a read model (tech-stack §5, §6, ADR 0039).
/// </summary>
[McpServerToolType]
public sealed class RegisterPartyTool
{
    /// <summary>The tool's name (ADR 0021).</summary>
    public const string Name = "party_reference_data_directory_register";

    /// <summary>The BIAN operation id, which names the tool's authorisation policy (ADR 0038).</summary>
    public const string OperationId = "PartyReferenceDataDirectoryEntry/Register";

    /// <summary>
    /// Registers a party, as the HTTP endpoint does: the same command, the same idempotency and the same errors.
    /// </summary>
    /// <param name="partyType">BIAN <c>PartyType</c>.</param>
    /// <param name="idempotencyKey">The command's idempotency key (ADR 0018).</param>
    /// <param name="bus">The host's message bus.</param>
    /// <param name="cancellationToken">The call's cancellation.</param>
    /// <returns>The new entry with its <c>version</c>, or the Problem Details of the error.</returns>
    [McpServerTool(Name = Name, Title = "Register a party", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [ToolOperation(OperationId)]
    [Description(
        "BIAN Party Reference Data Directory: register a customer entity in the catalog. Creates a directory entry for a "
        + "Person or an Organisation, with its Demographics, Reference and BankRelations, and returns their ids and the "
        + "entry's version (send it as expectedVersion to change the entry). The open date is the time of registration. "
        + "Calling again with the same idempotencyKey and arguments returns the same entry.")]
    public static async Task<CallToolResult> RegisterAsync(
        [Description("BIAN PartyType: Person or Organisation (case-sensitive).")] string partyType,
        [Description("A key of 1 to 255 visible ASCII characters, unique per registration. Reusing it with other arguments is a conflict.")] string idempotencyKey,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bus);

        var command = IdempotencyKey.TryCreate(idempotencyKey)
            .Bind(key => PartyTypes.FromBianName(partyType).Map(type => new RegisterParty(type, key)));
        if (command.IsFailure)
        {
            return ToolProblem.Create(command.Error);
        }

        var outcome = await bus.InvokeAsync<Result<RegisterPartyAcknowledged>>(command.Value, cancellationToken);
        return outcome.ToToolResult(acknowledged => ToolResults.Structured(RegisterPartyResult.From(acknowledged)));
    }
}
