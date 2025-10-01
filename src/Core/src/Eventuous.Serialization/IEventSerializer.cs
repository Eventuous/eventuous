// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public interface IEventSerializer {
    [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
    [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
    DeserializationResult DeserializeEvent(ReadOnlySpan<byte> data, string eventType, string contentType);

    [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
    [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
    SerializationResult SerializeEvent(object evt);

    internal const string SerializationUnreferencedCodeMessage =
        "JSON serialization and deserialization might require types that cannot be statically analyzed. Use DefaultStaticEventSerializer with System.Text.Json source generation for native AOT applications.";

    internal const string SerializationRequiresDynamicCodeMessage =
        "JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation. Use DefaultStaticEventSerializer with System.Text.Json source generation for native AOT applications.";
}

public record SerializationResult(string EventType, string ContentType, byte[] Payload);

public abstract record DeserializationResult {
    public record SuccessfullyDeserialized(object Payload) : DeserializationResult;

    public record FailedToDeserialize(DeserializationError Error) : DeserializationResult;
}

public enum DeserializationError {
    UnknownType,
    ContentTypeMismatch,
    PayloadEmpty
}
