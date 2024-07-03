// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

// ReSharper disable NotAccessedPositionalProperty.Global

namespace Eventuous;

[StructLayout(LayoutKind.Auto)]
public record struct Change(object Event, string EventType);

public record Result<TState> where TState : class, new() {
    ExceptionDispatchInfo? _exception;
    Ok?                    _value;
    string?                _errorMessage;

    private Result() { }

    public bool TryGet([NotNullWhen(true)] out Ok? value) {
        value = _value;

        return _exception is null;
    }

    public bool TryGetError([NotNullWhen(true)] out Error? error) {
        error = _exception is not null ? GetError() : null;

        return _exception is not null;
    }

    public static Result<TState> FromSuccess(TState state, IEnumerable<Change> changes, ulong streamPosition)
        => new() { _value = new(state, changes, streamPosition) };

    public static Result<TState> FromError(Exception exception)
        => new() {
            _exception    = ExceptionDispatchInfo.Capture(exception),
            _errorMessage = exception.Message
        };

    public static Result<TState> FromError(Exception? exception, string message)
        => new() {
            _exception    = exception != null ? ExceptionDispatchInfo.Capture(exception) : null,
            _errorMessage = message
        };

    public void ThrowIfError() {
        _exception?.Throw();

        if (_errorMessage != null) {
            throw new(_errorMessage);
        }
    }

    public T Match<T>(Func<Ok, T> matchOk, Func<Error, T> errorMatch) => TryGet(out var ok) ? matchOk(ok) : errorMatch(GetError());

    Error GetError() => new(_exception?.SourceException, _errorMessage!);

    public bool Success => _exception is null;

    public Exception? Exception => _exception?.SourceException;

    public static bool operator true(in Result<TState> result) => result._value is not null;

    public static bool operator false(in Result<TState> result) => result._value is null;

    public record Ok(TState State, IEnumerable<Change> Changes, ulong StreamPosition);

    public record Error(Exception? Exception, string ErrorMessage);
}
