// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Eventuous;

[PublicAPI]
public class DefaultMetadataSerializer(JsonSerializerOptions options) : IMetadataSerializer {
    public static IMetadataSerializer Instance { get; private set; } = new DefaultMetadataSerializer(new(JsonSerializerDefaults.Web));

    public static void SetDefaultSerializer(IMetadataSerializer serializer) => Instance = serializer;

    MetadataSourceGenerationContext _context = new(options);

    public byte[] Serialize(Metadata evt) => JsonSerializer.SerializeToUtf8Bytes(evt, _context.Metadata);

    /// <inheritdoc/>
    public Metadata? Deserialize(ReadOnlySpan<byte> bytes) {
        try {
            return JsonSerializer.Deserialize(bytes, _context.Metadata);
        } catch (JsonException e) {
            throw new MetadataDeserializationException(e);
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Metadata))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(bool))]
internal partial class MetadataSourceGenerationContext : JsonSerializerContext;