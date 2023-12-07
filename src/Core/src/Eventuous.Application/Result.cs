// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;

namespace Eventuous;

public record struct Change(object Event, string EventType);

public record Result : Result<object> {
    public Result() : base(null, false) { }
    public Result(object? State, bool Success, IEnumerable<Change>? Changes = null) : base(State, Success, Changes) { }
}

public record OkResult(object State, IEnumerable<Change>? Changes = null) : Result(State, true, Changes);

public record ErrorResult(string Message, [property: JsonIgnore] Exception? Exception) : Result(null, false) {
    public string ErrorMessage => Exception?.Message ?? "Unknown error";
}

[PublicAPI]
public abstract record Result<TState>(TState? State, bool Success, IEnumerable<Change>? Changes = null) where TState: class;

[PublicAPI]
public record OkResult<TState>(TState State, IEnumerable<Change> Changes, ulong StreamPosition)
    : Result<TState>(State, true, Changes) where TState : State<TState>;

[PublicAPI]
public record ErrorResult<TState> : Result<TState> where TState : State<TState> {
    public ErrorResult(string message, Exception? exception) : base(null, false) {
        Message   = message;
        Exception = exception;
    }

    public ErrorResult(Exception exception) : base(null, false) {
        Exception = Ensure.NotNull(exception);
        Message   = exception.Message;
    }

    public string Message { get; init; }

    [JsonIgnore] public Exception? Exception { get; init; }

    public string? ErrorMessage => Exception?.Message;
}
