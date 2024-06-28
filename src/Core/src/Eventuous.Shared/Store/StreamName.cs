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

    public static StreamName ForState<TState>(string entityId) {
        var stateName = typeof(TState).Name;

        if (stateName.EndsWith("State")) {
            stateName = stateName[..^SuffixLength];
        }

        stateName = stateName.Length > 0 ? stateName : typeof(TState).Name;

        return new($"{stateName}-{Ensure.NotEmptyString(entityId)}");
    }

    public readonly string GetId() => Value[(Value.IndexOf('-') + 1)..];

    public static implicit operator string(StreamName streamName) => streamName.Value;

    public override readonly string ToString() => Value;

    static readonly int SuffixLength = "State".Length;
}

public class InvalidStreamName(string? streamName) : Exception($"Stream name is {(string.IsNullOrWhiteSpace(streamName) ? "empty" : "invalid")}");
