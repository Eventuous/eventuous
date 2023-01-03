// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Eventuous;

static class Ensure {
    /// <summary>
    /// Checks if the object is not null, otherwise throws
    /// </summary>
    /// <param name="value">Object to check for null value</param>
    /// <param name="name">Name of the object to be used in the exception message</param>
    /// <typeparam name="T">Object type</typeparam>
    /// <returns>Non-null object value</returns>
    /// <exception cref="ArgumentNullException"></exception>
    [DebuggerHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T NotNull<T>(T? value, [CallerArgumentExpression("value")] string? name = default) where T : class
        => value ?? throw new ArgumentNullException(name);

    /// <summary>
    /// Checks if the string is not null or empty, otherwise throws
    /// </summary>
    /// <param name="value">String value to check</param>
    /// <param name="name">Name of the parameter to be used in the exception message</param>
    /// <returns>Non-null and not empty string</returns>
    /// <exception cref="ArgumentNullException"></exception>
    [DebuggerHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string NotEmptyString(string? value, [CallerArgumentExpression("value")] string? name = default)
        => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentNullException(name);

    // /// <summary>
    // /// Throws a <see cref="DomainException"/> with a given message if the condition is not met
    // /// </summary>
    // /// <param name="condition">Condition to check</param>
    // /// <param name="errorMessage">Message for the exception</param>
    // /// <exception cref="DomainException"></exception>
    // [DebuggerHidden]
    // public static void IsTrue(bool condition, Func<string> errorMessage) {
    //     if (!condition) throw new DomainException(errorMessage());
    // }

    /// <summary>
    /// Throws a custom exception if the condition is not met
    /// </summary>
    /// <param name="condition">Condition to check</param>
    /// <param name="getException"></param>
    /// <exception cref="Exception"></exception>
    [DebuggerHidden]
    public static void IsTrue(bool condition, Func<Exception> getException) {
        if (!condition) throw getException();
    }
}