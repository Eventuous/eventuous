using System.Text.Json;

namespace Eventuous; 

[PublicAPI]
public class DefaultMetadataSerializer : IMetadataSerializer {
    public static IMetadataSerializer Instance { get; private set; } =
        new DefaultMetadataSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static void SetDefaultSerializer(IMetadataSerializer serializer)
        => Instance = serializer;

    readonly JsonSerializerOptions _options;

    public DefaultMetadataSerializer(JsonSerializerOptions options)
        => _options = options;

    public byte[] Serialize(Metadata evt)
        => JsonSerializer.SerializeToUtf8Bytes(evt, _options);

    public Metadata? Deserialize(ReadOnlySpan<byte> bytes)
        => JsonSerializer.Deserialize<Metadata>(bytes, _options);
}