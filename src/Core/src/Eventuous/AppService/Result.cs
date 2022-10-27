// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;

namespace Eventuous;

[PublicAPI]
public record struct Change(object Event, string EventType);

[PublicAPI]
public abstract record Result(object? State, bool Success, IEnumerable<Change>? Changes = null);

[PublicAPI]
public record OkResult(object State, IEnumerable<Change>? Changes = null) : Result(State, true, Changes);

[PublicAPI]
public record ErrorResult : Result {
    public ErrorResult(string message, Exception? exception) : base(null, false) {
        Message   = message;
        Exception = exception;
    }

    [JsonIgnore]
    public Exception? Exception { get; }

    public string ErrorMessage => Exception?.Message ?? "Unknown error";

    public string Message { get; }
}

[PublicAPI]
public abstract record Result<TState>(TState? State, bool Success, IEnumerable<Change>? Changes = null)
    where TState : State<TState>, new();

[PublicAPI]
public record OkResult<TState>(TState State, IEnumerable<Change> Changes, ulong StreamPosition)
    : Result<TState>(State, true, Changes) where TState : State<TState>, new();

[PublicAPI]
public record ErrorResult<TState> : Result<TState> where TState : State<TState>, new() {
    public ErrorResult(string message, Exception? exception) : base(null, false) {
        Message   = message;
        Exception = exception;
    }

    public ErrorResult(Exception exception) : base(null, false) {
        Exception = Ensure.NotNull(exception);
        Message   = exception.Message;
    }

    public string Message { get; init; }

    [JsonIgnore]
    public Exception? Exception { get; init; }

    public string? ErrorMessage => Exception?.Message;
}
