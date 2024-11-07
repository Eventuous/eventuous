// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;

namespace Eventuous.ElasticSearch.Store;

[ElasticsearchType(IdProperty = "MessageId")]
[EventType("Event")]
public record PersistedEvent {
    // ReSharper disable once ConvertToPrimaryConstructor
    public PersistedEvent(
        string                       messageId,
        string                       messageType,
        long                         streamPosition,
        string                       contentType,
        string                       stream,
        ulong                        globalPosition,
        object?                      message,
        Dictionary<string, string?>? metadata,
        DateTime                     created
    ) {
        MessageId      = messageId;
        MessageType    = messageType;
        StreamPosition = streamPosition;
        ContentType    = contentType;
        Stream         = stream;
        GlobalPosition = globalPosition;
        Message        = message;
        Metadata       = metadata;
        Created        = created;
    }

    public string MessageId      { get; }
    public string MessageType    { get; }
    public long   StreamPosition { get; }
    public string ContentType    { get; }

    [Keyword]
    public string Stream { get; }

    [PublicAPI]
    public ulong                        GlobalPosition { get; }
    public object?                      Message        { get; init; }
    public Dictionary<string, string?>? Metadata       { get; }

    [Date(Name = "@timestamp")]
    [JsonPropertyName("@timestamp")]
    [PublicAPI]
    public DateTime Created { get; }
}