// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using static Eventuous.DeserializationResult;

namespace Eventuous;

[PublicAPI]
public class DefaultEventSerializer(JsonSerializerOptions options, ITypeMapper? typeMapper = null) : IEventSerializer {
    public static IEventSerializer Instance { get; private set; } = new DefaultEventSerializer(new(JsonSerializerDefaults.Web));

    readonly ITypeMapper _typeMapper = typeMapper ?? TypeMap.Instance;

    public static void SetDefaultSerializer(IEventSerializer serializer) => Instance = serializer;

    public DeserializationResult DeserializeEvent(ReadOnlySpan<byte> data, string eventType, string contentType) {
        var typeMapped = _typeMapper.TryGetType(eventType, out var dataType);

        if (!typeMapped) return new FailedToDeserialize(DeserializationError.UnknownType);
        if (contentType != ContentType) return new FailedToDeserialize(DeserializationError.ContentTypeMismatch);

        var deserialized = JsonSerializer.Deserialize(data, dataType!, options);

        return deserialized != null
            ? new SuccessfullyDeserialized(deserialized)
            : new FailedToDeserialize(DeserializationError.PayloadEmpty);
    }

    public SerializationResult SerializeEvent(object evt)
        => new(_typeMapper.GetTypeName(evt), ContentType, JsonSerializer.SerializeToUtf8Bytes(evt, options));

    public string ContentType { get; } = "application/json";
}
