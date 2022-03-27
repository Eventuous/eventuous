namespace Eventuous.Kafka.Producers; 

public static class DefaultRouters {
    internal static MessageRoute RouteByCategory(string stream) {
        var catIndex = stream.IndexOf('-');

        var topic = catIndex >= 0 ? stream[..catIndex] : stream;
        return new MessageRoute(topic, stream);
    }
}

public record MessageRoute(string Topic, string PartitionKey);