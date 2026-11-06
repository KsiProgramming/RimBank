using YamlDotNet.RepresentationModel;

namespace RimBank.Conformance.Testing;

/// <summary>
/// Reads the channels of a BIAN ext-DDD AsyncAPI 3 (YAML) specification with YamlDotNet (ADR 0020). Only
/// <c>channels.*.address</c> is read; every other field is ignored. A channel without an address uses its key.
/// </summary>
internal static class AsyncApiSpecificationReader
{
    public static async Task<IReadOnlyList<BianChannel>> ReadAsync(FileInfo file, CancellationToken cancellationToken)
    {
        var yaml = new YamlStream();
        using (var reader = file.OpenText())
        {
            yaml.Load(new StringReader(await reader.ReadToEndAsync(cancellationToken)));
        }

        if (yaml.Documents.FirstOrDefault()?.RootNode is not YamlMappingNode root
            || !root.Children.TryGetValue(new YamlScalarNode("channels"), out var channelsNode)
            || channelsNode is not YamlMappingNode channels)
        {
            throw new InvalidOperationException($"The AsyncAPI specification '{file.FullName}' has no 'channels' mapping.");
        }

        return
        [
            .. channels.Children.Select(channel => new BianChannel(AddressOf(channel.Key, channel.Value))),
        ];
    }

    private static string AddressOf(YamlNode key, YamlNode channel) =>
        channel is YamlMappingNode mapping
            && mapping.Children.TryGetValue(new YamlScalarNode("address"), out var address)
            && address is YamlScalarNode { Value: { Length: > 0 } value }
            ? value
            : ((YamlScalarNode)key).Value ?? string.Empty;
}
