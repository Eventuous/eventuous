using System;
using System.Text.Json;
using JetBrains.Annotations;

namespace Eventuous {
    [PublicAPI]
    public class DefaultEventSerializer : IEventSerializer {
        public static readonly IEventSerializer Instance =
            new DefaultEventSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        readonly JsonSerializerOptions _options;

        public DefaultEventSerializer(JsonSerializerOptions options) => _options = options;

        public object? Deserialize(ReadOnlySpan<byte> data, string eventType)
            => !TypeMap.TryGetType(eventType, out var dataType)
                ? null!
                : JsonSerializer.Deserialize(data, dataType!, _options);

        public byte[] Serialize(object evt) => JsonSerializer.SerializeToUtf8Bytes(evt, _options);
    }
}