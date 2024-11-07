// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions;

using Logging;

public static class MetaSerializerExtensions {
    public static Metadata? DeserializeMeta(
        this IMetadataSerializer? metaSerializer,
        SubscriptionOptions       options,
        ReadOnlyMemory<byte>      meta,
        string                    stream,
        ulong                     position = 0
    ) {
        if (meta.IsEmpty) return null;

        try {
            return (metaSerializer ?? DefaultMetadataSerializer.Instance).Deserialize(meta.Span);
        }
        catch (Exception e) {
            Logger.Current.MetadataDeserializationFailed(stream, position, e);

            if (options.ThrowOnError) throw new DeserializationException(stream, "metadata", position, e);

            return null;
        }
    }
}
