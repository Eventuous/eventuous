// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using static Eventuous.DeserializationResult;

namespace Eventuous;

[PublicAPI]
public class DefaultEventSerializer : IEventSerializer {
    const string DynamicSerializationMessage =
        "DefaultEventSerializer uses reflection-based System.Text.Json serialization. Use DefaultStaticEventSerializer with a JsonSerializerContext in trimmed or AOT applications.";

    readonly JsonSerializerOptions _options;
    readonly ITypeMapper           _typeMapper;

    [RequiresUnreferencedCode(DynamicSerializationMessage)]
    [RequiresDynamicCode(DynamicSerializationMessage)]
    public DefaultEventSerializer(JsonSerializerOptions options, ITypeMapper? typeMapper = null) {
        _options    = options;
        _typeMapper = typeMapper ?? TypeMap.Instance;

        // Auto-register as default if none is set
        EventSerializer.TrySetDefault(this);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The constructor is annotated with RequiresUnreferencedCode, so an instance only exists if the caller acknowledged the requirement")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The constructor is annotated with RequiresDynamicCode, so an instance only exists if the caller acknowledged the requirement")]
    public DeserializationResult DeserializeEvent(ReadOnlySpan<byte> data, string eventType, string contentType) {
        var typeMapped = _typeMapper.TryGetType(eventType, out var dataType);

        if (!typeMapped) return new FailedToDeserialize(DeserializationError.UnknownType);
        if (contentType != ContentType) return new FailedToDeserialize(DeserializationError.ContentTypeMismatch);

        var deserialized = JsonSerializer.Deserialize(data, dataType!, _options);

        return deserialized != null
            ? new SuccessfullyDeserialized(deserialized)
            : new FailedToDeserialize(DeserializationError.PayloadEmpty);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The constructor is annotated with RequiresUnreferencedCode, so an instance only exists if the caller acknowledged the requirement")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The constructor is annotated with RequiresDynamicCode, so an instance only exists if the caller acknowledged the requirement")]
    public SerializationResult SerializeEvent(object evt)
        => new(_typeMapper.GetTypeName(evt), ContentType, JsonSerializer.SerializeToUtf8Bytes(evt, _options));

    public string ContentType { get; } = "application/json";
}
