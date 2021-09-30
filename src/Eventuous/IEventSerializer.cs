namespace Eventuous; 

public interface IEventSerializer {
    object? DeserializeEvent(ReadOnlySpan<byte> data, string eventType);

    (string EventType, byte[] Payload) SerializeEvent(object evt);
        

    string ContentType { get; }
}