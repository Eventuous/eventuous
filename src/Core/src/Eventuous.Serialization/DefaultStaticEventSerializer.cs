// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using System.Text.Json.Serialization;
using static Eventuous.DeserializationResult;

namespace Eventuous;

[PublicAPI]
public class DefaultStaticEventSerializer(JsonSerializerContext context, ITypeMapper? typeMapper = null) : IEventSerializer {
    // Failure results are immutable, so one instance per error kind is shared instead of allocating per event
    static readonly FailedToDeserialize UnknownType         = new(DeserializationError.UnknownType);
    static readonly FailedToDeserialize ContentTypeMismatch = new(DeserializationError.ContentTypeMismatch);
    static readonly FailedToDeserialize PayloadEmpty        = new(DeserializationError.PayloadEmpty);

    readonly ITypeMapper _typeMapper = typeMapper ?? TypeMap.Instance;

    public DeserializationResult DeserializeEvent(ReadOnlySpan<byte> data, string eventType, string contentType) {
        var typeMapped = _typeMapper.TryGetType(eventType, out var dataType);

        if (!typeMapped) return UnknownType;
        if (contentType != ContentType) return ContentTypeMismatch;

        var deserialized = JsonSerializer.Deserialize(data, dataType!, context);

        return deserialized != null
            ? new SuccessfullyDeserialized(deserialized)
            : PayloadEmpty;
    }

    public SerializationResult SerializeEvent(object evt)
        => new(_typeMapper.GetTypeName(evt), ContentType, JsonSerializer.SerializeToUtf8Bytes(evt, evt.GetType(), context));

    public string ContentType { get; } = "application/json";
}
