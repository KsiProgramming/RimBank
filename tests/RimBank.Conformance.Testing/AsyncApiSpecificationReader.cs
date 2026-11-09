using System.Text.Json.Nodes;

namespace RimBank.Conformance.Testing;

/// <summary>
/// Reads the channels of a BIAN ext-DDD AsyncAPI 3 (YAML) specification (ADR 0020, ADR 0037): each channel's
/// <c>address</c> (its key when it has none) and the payload schema of its first message. Every other field is ignored.
/// </summary>
internal static class AsyncApiSpecificationReader
{
    public static async Task<IReadOnlyList<BianChannel>> ReadAsync(FileInfo file, CancellationToken cancellationToken)
    {
        var tree = await SpecificationDocument.LoadAsync(file, cancellationToken);
        if (tree.Root["channels"] is not JsonObject channels)
        {
            throw new InvalidOperationException($"The AsyncAPI specification '{file.FullName}' has no 'channels' mapping.");
        }

        return
        [
            .. channels.Select(channel => new BianChannel(AddressOf(channel.Key, channel.Value))
            {
                Payload = PayloadOf(tree, channel.Value),
            }),
        ];
    }

    private static string AddressOf(string key, JsonNode? channel) =>
        channel?["address"] is JsonValue address && address.GetValue<string>() is { Length: > 0 } value ? value : key;

    private static BianSchema? PayloadOf(SpecificationDocument tree, JsonNode? channel)
    {
        if (tree.Resolve(channel)?["messages"] is not JsonObject { Count: > 0 } messages)
        {
            return null;
        }

        var message = tree.Resolve(messages.First().Value);
        return BianSchema.From(tree, message?["payload"], (message?["name"] as JsonValue)?.GetValue<string>() ?? messages.First().Key);
    }
}
