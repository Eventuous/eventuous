using System.Text;
using Confluent.Kafka;

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
}
