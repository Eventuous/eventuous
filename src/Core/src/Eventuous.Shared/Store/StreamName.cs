// Copyright (C) Eventuous HQ OÜ. All rights reserved
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
        var id            = Ensure.NotEmptyString(entityId).Trim();
        var stateName     = typeof(TState).Name;
        var stateNameSpan = stateName.AsSpan();

        if (stateNameSpan.EndsWith("State")) {
            stateNameSpan = stateNameSpan[..^SuffixLength];
        }

        return stateNameSpan.Length > 0 ? new($"{stateNameSpan}-{id}") : new($"{stateName}-{id}");
    }

    public readonly string GetId() => Value[(Value.IndexOf('-') + 1)..];

    public static implicit operator string(StreamName streamName) => streamName.Value;

    public override readonly string ToString() => Value;

    static readonly int SuffixLength = "State".Length;
}

public class InvalidStreamName(string? streamName) : Exception($"Stream name is {(string.IsNullOrWhiteSpace(streamName) ? "empty" : "invalid")}");
