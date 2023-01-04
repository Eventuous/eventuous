// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

[PublicAPI]
public record struct StreamName {
    string Value { get; }

    public StreamName(string value) {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidStreamName(value);

        Value = value;
    }

    public static StreamName For<T>(string entityId) => new($"{typeof(T).Name}-{Ensure.NotEmptyString(entityId)}");

    public string GetId() => Value[(Value.IndexOf("-", StringComparison.InvariantCulture) + 1)..];
    
    public static implicit operator string(StreamName streamName) => streamName.Value;

    public override string ToString() => Value;
}

public class InvalidStreamName : Exception {
    public InvalidStreamName(string? streamName)
        : base($"Stream name is {(string.IsNullOrWhiteSpace(streamName) ? "empty" : "invalid")}") { }
}
