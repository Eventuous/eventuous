// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;

namespace Eventuous;

[PublicAPI]
public class DefaultEventSerializer : IEventSerializer {
    public static IEventSerializer Instance { get; private set; } =
        new DefaultEventSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    readonly JsonSerializerOptions _options;
    readonly TypeMapper            _typeMapper;

    public static void SetDefaultSerializer(IEventSerializer serializer) => Instance = serializer;

    public DefaultEventSerializer(JsonSerializerOptions options, TypeMapper? typeMapper = null) {
        _options    = options;
        _typeMapper = typeMapper ?? TypeMap.Instance;
    }

    public DeserializationResult DeserializeEvent(ReadOnlySpan<byte> data, string eventType, string contentType) {
        var typeMapped = _typeMapper.TryGetType(eventType, out var dataType);
        if (!typeMapped) return new FailedToDeserialize(DeserializationError.UnknownType);
        if (contentType != ContentType) return new FailedToDeserialize(DeserializationError.ContentTypeMismatch);

        var deserialized = JsonSerializer.Deserialize(data, dataType!, _options);

        return deserialized != null 
            ? new SuccessfullyDeserialized(deserialized)
            : new FailedToDeserialize(DeserializationError.PayloadEmpty);
    }

    public SerializationResult SerializeEvent(object evt)
        => new(_typeMapper.GetTypeName(evt), ContentType, JsonSerializer.SerializeToUtf8Bytes(evt, _options));

    public string ContentType { get; } = "application/json";
}