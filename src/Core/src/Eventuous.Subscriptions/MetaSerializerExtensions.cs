// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using static Eventuous.Subscriptions.Diagnostics.SubscriptionsEventSource;

namespace Eventuous.Subscriptions;

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
            Log.MetadataDeserializationFailed(options.SubscriptionId, stream, position, e.ToString());

            if (options.ThrowOnError) throw new DeserializationException(stream, "metadata", position, e);

            return null;
        }
    }
}
