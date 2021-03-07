using System;
using System.Text.Json;

namespace CoreLib {
    public class DefaultEventSerializer : IEventSerializer {
        readonly JsonSerializerOptions _options;

        public DefaultEventSerializer(JsonSerializerOptions options) => _options = options;

        public object Deserialize(ReadOnlySpan<byte> data, string eventType)
            => !TypeMap.TryGetType(eventType, out var dataType)
                ? null
                : JsonSerializer.Deserialize(data, dataType, _options);

        public byte[] Serialize(object evt) => JsonSerializer.SerializeToUtf8Bytes(evt);
    }
}