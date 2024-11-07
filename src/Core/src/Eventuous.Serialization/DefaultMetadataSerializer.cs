// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;

namespace Eventuous;

[PublicAPI]
public class DefaultMetadataSerializer(JsonSerializerOptions options) : IMetadataSerializer {
    public static IMetadataSerializer Instance { get; private set; } = new DefaultMetadataSerializer(new(JsonSerializerDefaults.Web));

    public static void SetDefaultSerializer(IMetadataSerializer serializer) => Instance = serializer;

    public byte[] Serialize(Metadata evt) => JsonSerializer.SerializeToUtf8Bytes(evt, options);

    /// <inheritdoc/>
    public Metadata? Deserialize(ReadOnlySpan<byte> bytes) {
        try {
            return JsonSerializer.Deserialize<Metadata>(bytes, options);
        } catch (JsonException e) {
            throw new MetadataDeserializationException(e);
        }
    }
}
