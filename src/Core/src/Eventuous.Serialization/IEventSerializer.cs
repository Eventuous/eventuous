// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public interface IEventSerializer {
    DeserializationResult DeserializeEvent(ReadOnlySpan<byte> data, string eventType, string contentType);

    SerializationResult SerializeEvent(object evt);
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
