using System.Text.Json.Serialization;
using Nest;

namespace Eventuous.ElasticSearch.Store;

class ElasticMeta {
    public static Dictionary<string, string?>? FromMetadata(Metadata? metadata)
        => metadata?.ToDictionary(x => x.Key, x => x.Value?.ToString());
}

[ElasticsearchType(IdProperty = "MessageId")]
[EventType("Event")]
public record PersistedEvent {
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

    public ulong                        GlobalPosition { get; }
    public object?                      Message        { get; init; }
    public Dictionary<string, string?>? Metadata       { get; }

    [Date(Name = "@timestamp")]
    [JsonPropertyName("@timestamp")]
    public DateTime Created { get; }

    public void Deconstruct(
        out string                       messageId,
        out string                       messageType,
        out long                         streamPosition,
        out string                       contentType,
        out string                       stream,
        out ulong                        globalPosition,
        out object?                      message,
        out Dictionary<string, string?>? metadata,
        out DateTime                     created
    ) {
        messageId      = MessageId;
        messageType    = MessageType;
        streamPosition = StreamPosition;
        contentType    = ContentType;
        stream         = Stream;
        globalPosition = GlobalPosition;
        message        = Message;
        metadata       = Metadata;
        created        = Created;
    }
}
