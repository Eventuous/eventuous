// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using System.Text.Json.Serialization;
using static Eventuous.DeserializationResult;

namespace Eventuous;

[PublicAPI]
public class DefaultStaticEventSerializer(JsonSerializerContext context, ITypeMapper? typeMapper = null) : IEventSerializer {
    readonly ITypeMapper _typeMapper = typeMapper ?? TypeMap.Instance;

    [UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "This implementation is not using reflection.")]
    [UnconditionalSuppressMessage("Trimming", "IL3051", Justification = "This implementation is not using reflection.")]
    public DeserializationResult DeserializeEvent(ReadOnlySpan<byte> data, string eventType, string contentType) {
        var typeMapped = _typeMapper.TryGetType(eventType, out var dataType);

        if (!typeMapped) return new FailedToDeserialize(DeserializationError.UnknownType);
        if (contentType != ContentType) return new FailedToDeserialize(DeserializationError.ContentTypeMismatch);

        var deserialized = JsonSerializer.Deserialize(data, dataType!, context);

        return deserialized != null
            ? new SuccessfullyDeserialized(deserialized)
            : new FailedToDeserialize(DeserializationError.PayloadEmpty);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "This implementation is not using reflection.")]
    [UnconditionalSuppressMessage("Trimming", "IL3051", Justification = "This implementation is not using reflection.")]
    public SerializationResult SerializeEvent(object evt)
        => new(_typeMapper.GetTypeName(evt), ContentType, JsonSerializer.SerializeToUtf8Bytes(evt, evt.GetType(), context));

    public string ContentType { get; } = "application/json";
}
