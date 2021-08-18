using System;
using System.Text.Json;
using JetBrains.Annotations;

namespace Eventuous {
    [PublicAPI]
    public class DefaultEventSerializer : IEventSerializer {
        public static IEventSerializer Instance { get; private set; } =
            new DefaultEventSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        readonly JsonSerializerOptions _options;
        readonly TypeMapper            _typeMapper;

        public static void SetDefaultSerializer(IEventSerializer serializer)
            => Instance = serializer;

        public DefaultEventSerializer(JsonSerializerOptions options, TypeMapper? typeMapper = null) {
            _options    = options;
            _typeMapper = typeMapper ?? TypeMap.Instance;
        }

        public object? DeserializeEvent(ReadOnlySpan<byte> data, string eventType)
            => !_typeMapper.TryGetType(eventType, out var dataType)
                ? null!
                : JsonSerializer.Deserialize(data, dataType!, _options);

        public (string EventType, byte[] Payload) SerializeEvent(object evt)
            => (_typeMapper.GetTypeName(evt), JsonSerializer.SerializeToUtf8Bytes(evt, _options));

        public byte[] SerializeMetadata(Metadata evt) => JsonSerializer.SerializeToUtf8Bytes(evt, _options);

        public string ContentType { get; } = "application/json";
    }
}