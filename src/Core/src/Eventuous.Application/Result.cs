// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

// ReSharper disable NotAccessedPositionalProperty.Global

namespace Eventuous;

[StructLayout(LayoutKind.Auto)]
public record struct Change(object Event, string EventType) {
    internal static Change FromEvent(object evt, ITypeMapper typeMapper) {
        var typeName = typeMapper.GetTypeName(evt);

        return new(evt, typeName != ITypeMapper.UnknownType ? typeName : evt.GetType().Name);
    }
}

/// <summary>
/// Represents the command handling result, could be either success or error
/// </summary>
/// <typeparam name="TState"></typeparam>
public record Result<TState> where TState : class, new() {
    ExceptionDispatchInfo? _exception;
    Ok?                    _value;
    string?                _errorMessage;

    private Result() { }

    /// <summary>
    /// Try to get the successful result value
    /// </summary>
    /// <param name="value">Successful result if available</param>
    /// <returns>True if the result is successful, false if it's an error</returns>
    public bool TryGet([NotNullWhen(true)] out Ok? value) {
        value = _value;

        return _exception is null;
    }

    /// <summary>
    /// Returns the successful result if available or null if it's an error
    /// </summary>
    /// <returns>Successful result or null</returns>
    public Ok? Get() => _value;

    /// <summary>
    /// Try to get the error if available
    /// </summary>
    /// <param name="error">Error result if available</param>
    /// <returns>True if the result is an error, false otherwise</returns>
    public bool TryGetError([NotNullWhen(true)] out Error? error) {
        error = _exception is not null ? GetError() : null;

        return _exception is not null;
    }

    Error GetError() => new(_exception?.SourceException, _errorMessage!);

    /// <summary>
    /// Creates a result instance from successful command handling
    /// </summary>
    /// <param name="state">State instance</param>
    /// <param name="changes">List of new events</param>
    /// <param name="globalPosition">Global position of the last new event in the log</param>
    /// <returns>New result instance</returns>
    public static Result<TState> FromSuccess(TState state, IEnumerable<Change> changes, ulong globalPosition)
        => new() { _value = new(state, changes, globalPosition) };

    /// <summary>
    /// Creates a result instance from an error
    /// </summary>
    /// <param name="exception">Exception that occured during command handling process</param>
    /// <returns>New result instance with an error</returns>
    public static Result<TState> FromError(Exception exception)
        => new() {
            _exception    = ExceptionDispatchInfo.Capture(exception),
            _errorMessage = exception.Message
        };

    /// <summary>
    /// Creates a result instance from an error
    /// </summary>
    /// <param name="exception">Exception that occured during command handling process</param>
    /// <param name="message">Error message that explains what the error is</param>
    /// <returns>New result instance with an error</returns>
    public static Result<TState> FromError(Exception? exception, string message)
        => new() {
            _exception    = exception != null ? ExceptionDispatchInfo.Capture(exception) : null,
            _errorMessage = message
        };

    /// <summary>
    /// Checks if the result is an error and throws a captured exception
    /// </summary>
    /// <exception cref="Exception">Non-specific exception is thrown if there's no captured exception available</exception>
    public void ThrowIfError() {
        _exception?.Throw();

        if (_errorMessage != null) {
            throw new(_errorMessage);
        }
    }

    /// <summary>
    /// Pattern-match the result and transforms it to a new value of a single type
    /// </summary>
    /// <param name="matchOk">Function to produce output from a successful result</param>
    /// <param name="matchError">Function to produce output value from an error</param>
    /// <typeparam name="T">Output type</typeparam>
    /// <returns></returns>
    public T Match<T>(Func<Ok, T> matchOk, Func<Error, T> matchError) => TryGet(out var ok) ? matchOk(ok) : matchError(GetError());

    /// <summary>
    /// Pattern-match the result and execute a function based on the result type
    /// </summary>
    /// <param name="matchOk">Function to be executed on a successful result</param>
    /// <param name="matchError">Function to be executed on an error</param>
    public void Match(Action<Ok> matchOk, Action<Error> matchError) {
        if (TryGet(out var ok)) { matchOk(ok); }
        else { matchError(GetError()); }
    }

    /// <summary>
    /// Pattern-match the result and execute an async function based on the result type
    /// </summary>
    /// <param name="matchOk">Function to be executed on a successful result</param>
    /// <param name="matchError">Function to be executed on an error</param>
    public async Task MatchAsync(Func<Ok, Task> matchOk, Func<Error, Task> matchError) {
        if (TryGet(out var ok)) { await matchOk(ok).NoContext(); }
        else { await matchError(GetError()).NoContext(); }
    }

    /// <summary>
    /// Indicates if the result is successful
    /// </summary>
    public bool Success => _exception is null;

    /// <summary>
    /// Returns the exception that caused the error if available
    /// </summary>
    public Exception? Exception => _exception?.SourceException;

    public static bool operator true(in Result<TState> result) => result._value is not null;

    public static bool operator false(in Result<TState> result) => result._value is null;

    /// <summary>
    /// State of a successful result
    /// </summary>
    /// <param name="State">New state instance</param>
    /// <param name="Changes">Collection of new events</param>
    /// <param name="GlobalPosition">Global position of the last new event in the log</param>
    public record Ok(TState State, IEnumerable<Change> Changes, ulong GlobalPosition);

    /// <summary>
    /// State of an error result
    /// </summary>
    /// <param name="Exception">Captured exception</param>
    /// <param name="ErrorMessage">Error message</param>
    public record Error(Exception? Exception, string ErrorMessage);
}
