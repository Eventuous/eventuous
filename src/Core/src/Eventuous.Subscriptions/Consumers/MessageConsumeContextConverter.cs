// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Eventuous.Subscriptions.Logging;
using Conversion = System.Func<Eventuous.Subscriptions.Context.IMessageConsumeContext, object>;
using ContextConversion = System.Func<Eventuous.Subscriptions.Context.IMessageConsumeContext, Eventuous.Subscriptions.Context.IMessageConsumeContext?>;

namespace Eventuous.Subscriptions.Consumers;

using System.Diagnostics.CodeAnalysis;
using Context;

/// <summary>
/// Converts non-generic IMessageConsumeContext to a typed IMessageConsumeContext.
/// By default, it uses a cached, compiled expression-based constructor invocation.
/// External code (including source generators) can register fast-path converters
/// via <see cref="Register"/>, which will be attempted before using reflection.
/// </summary>
public static class MessageConsumeContextConverter {
    internal static readonly ConcurrentDictionary<Type, Conversion> ConversionCache = new();

    /// <summary>
    /// Copy-on-write: <see cref="Register"/> swaps in a new array, so a conversion running while a module
    /// initializer registers a converter reads a complete snapshot, never a list being resized.
    /// </summary>
    static volatile ContextConversion[] registeredConverters = [];

    internal static IReadOnlyList<ContextConversion> RegisteredConverters => registeredConverters;

    /// <summary>
    /// Registers a converter function to try before the fallback reflection-based conversion.
    /// Typical usage: a source generator emits a ModuleInitializer that calls Register with a
    /// generated converter that handles the known message types in the compilation.
    /// </summary>
    /// <param name="converter">A function that returns a typed context or null if not handled.</param>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public static void Register(ContextConversion converter) {
        registeredConverters = [.. registeredConverters, converter];
    }

    public static IMessageConsumeContext ConvertToGeneric(this IMessageConsumeContext context, InternalLogger? log = null) {
        var messageType = context.Message!.GetType();
        var converters  = registeredConverters;

        for (var i = 0; i < converters.Length; i++) {
            if (converters[i](context) is { } typedContext) {
                return typedContext;
            }
        }

        if (!ConversionCache.TryGetValue(messageType, out var conversion)) {
            log?.Log("Static context conversion not found for message type {MessageType}, using reflections. Consider opening a GitHub issue to help improving the generator", messageType);

            // Racing callers may both compile a conversion; only one gets cached, and either works
            conversion = ConversionCache.GetOrAdd(messageType, CreateConversionFunction);
        }

        return (IMessageConsumeContext)conversion(context);
    }

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "This should not be used because all the conversions should be pre-generated")]
    static Conversion CreateConversionFunction(Type messageType) {
        var contextType   = typeof(MessageConsumeContext<>).MakeGenericType(messageType);
        var contextParam  = Expression.Parameter(typeof(IMessageConsumeContext), "context");
        var newExpression = Expression.New(contextType.GetConstructor([typeof(IMessageConsumeContext)])!, contextParam);

        return Expression.Lambda<Conversion>(newExpression, contextParam).Compile();
    }
}
