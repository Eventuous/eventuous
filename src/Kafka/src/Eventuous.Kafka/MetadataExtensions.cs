// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text;

namespace Eventuous.Kafka;

static class MetadataExtensions {
    public static Headers AsKafkaHeaders(this Metadata metadata) {
        var headers = new Headers();

        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        foreach (var entry in metadata) {
            if (entry.Key == MetaTags.MessageId) continue;

            headers.AddHeader(entry.Key, entry.Value?.ToString());
        }

        return headers;
    }

    public static Headers AddHeader(this Headers headers, string key, string? value) {
        if (value != null) {
            headers.Add(key, Encoding.UTF8.GetBytes(value));
        }
        return headers;
    }
    
    public static Metadata AsMetadata(this Headers headers) {
        var metadata = new Metadata();

        foreach (var header in headers) {
            metadata.Add(header.Key, Encoding.UTF8.GetString(header.GetValueBytes()));
        }

        return metadata;
    }
}
