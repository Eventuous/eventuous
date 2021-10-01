namespace Eventuous.GooglePubSub.Producers;

[PublicAPI]
public class PubSubAttributes {
    public string EventType   { get; set; } = "eventType";
    public string ContentType { get; set; } = "contentType";
}