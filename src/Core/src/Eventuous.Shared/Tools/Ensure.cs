// Copyright (C) Eventuous HQ OÜ. All rights reserved
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
    public static T NotNull<T>(T? value, [CallerArgumentExpression("value")] string? name = default) where T : class {
        ArgumentNullException.ThrowIfNull(value, name);

        return value;
    }

    [DebuggerHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T NotNull<T>(T? value, [CallerArgumentExpression("value")] string? name = default) where T : struct {
        ArgumentNullException.ThrowIfNull(value, name);

        return value.Value;
    }

    /// <summary>
    /// Checks if the string is not null or empty, otherwise throws
    /// </summary>
    /// <param name="value">String value to check</param>
    /// <param name="name">Name of the parameter to be used in the exception message</param>
    /// <returns>Non-null and not empty string</returns>
    /// <exception cref="ArgumentNullException"></exception>
    [DebuggerHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string NotEmptyString(string? value, [CallerArgumentExpression("value")] string? name = default) {
#if NET8_0_OR_GREATER
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);

        return value;
#else
        return value is null ? throw new ArgumentNullException(name) : !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException(name);
#endif
    }

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
