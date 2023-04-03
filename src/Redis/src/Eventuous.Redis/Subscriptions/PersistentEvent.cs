namespace Eventuous.Redis;

public record PersistentEvent(
    Guid     MessageId,
    string   MessageType,
    long      StreamPosition,
    long     GlobalPosition,
    string   JsonData,
    string?  JsonMetadata,
    DateTime Created,
    string  StreamName
);