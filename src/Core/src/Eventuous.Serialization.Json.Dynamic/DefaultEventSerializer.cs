// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using static Eventuous.DeserializationResult;

namespace Eventuous;

[PublicAPI]
public class DefaultEventSerializer : IEventSerializer {
    const string DynamicSerializationMessage =
        "DefaultEventSerializer uses reflection-based System.Text.Json serialization. Use DefaultStaticEventSerializer with a JsonSerializerContext in trimmed or AOT applications.";

    // Failure results are immutable, so one instance per error kind is shared instead of allocating per event
    static readonly FailedToDeserialize UnknownType         = new(DeserializationError.UnknownType);
    static readonly FailedToDeserialize ContentTypeMismatch = new(DeserializationError.ContentTypeMismatch);
    static readonly FailedToDeserialize PayloadEmpty        = new(DeserializationError.PayloadEmpty);

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

        if (!typeMapped) return UnknownType;
        if (contentType != ContentType) return ContentTypeMismatch;

        var deserialized = JsonSerializer.Deserialize(data, dataType!, _options);

        return deserialized != null
            ? new SuccessfullyDeserialized(deserialized)
            : PayloadEmpty;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The constructor is annotated with RequiresUnreferencedCode, so an instance only exists if the caller acknowledged the requirement")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The constructor is annotated with RequiresDynamicCode, so an instance only exists if the caller acknowledged the requirement")]
    public SerializationResult SerializeEvent(object evt)
        => new(_typeMapper.GetTypeName(evt), ContentType, JsonSerializer.SerializeToUtf8Bytes(evt, _options));

    public string ContentType { get; } = "application/json";
}
