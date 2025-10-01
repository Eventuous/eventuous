// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Eventuous.Subscriptions.Logging;
using Conversion = System.Func<Eventuous.Subscriptions.Context.IMessageConsumeContext, object>;
using ContextConversion = System.Func<Eventuous.Subscriptions.Context.IMessageConsumeContext, Eventuous.Subscriptions.Context.IMessageConsumeContext?>;

namespace Eventuous.Subscriptions.Consumers;

using System.Diagnostics.CodeAnalysis;
using Context;
#if NET8_0
using Lock = object;
#endif

/// <summary>
/// Converts non-generic IMessageConsumeContext to a typed IMessageConsumeContext.
/// By default, it uses a cached, compiled expression-based constructor invocation.
/// External code (including source generators) can register fast-path converters
/// via <see cref="Register"/>, which will be attempted before using reflection.
/// </summary>
public static class MessageConsumeContextConverter {
    internal static readonly Dictionary<Type, Conversion?> ConversionCache      = new();
    internal static readonly List<ContextConversion>       RegisteredConverters = [];
    static readonly          Lock                          CacheLock            = new();

    /// <summary>
    /// Registers a converter function to try before the fallback reflection-based conversion.
    /// Typical usage: a source generator emits a ModuleInitializer that calls Register with a
    /// generated converter that handles the known message types in the compilation.
    /// </summary>
    /// <param name="converter">A function that returns a typed context or null if not handled.</param>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public static void Register(ContextConversion converter) {
        RegisteredConverters.Add(converter);
    }

    public static IMessageConsumeContext ConvertToGeneric(this IMessageConsumeContext context, InternalLogger? log = null) {
        var messageType = context.Message!.GetType();

        // ReSharper disable InconsistentlySynchronizedField
        if (RegisteredConverters.Count > 0) {
            for (var i = 0; i < RegisteredConverters.Count; i++) {
                var converter = RegisteredConverters[i];

                if (converter(context) is { } typedContext) {
                    return typedContext;
                }
            }
        }
        // ReSharper restore InconsistentlySynchronizedField

        // ReSharper disable once InconsistentlySynchronizedField
        if (!ConversionCache.TryGetValue(messageType, out var conversion)) {
            log?.Log("Static context conversion not found for message type {MessageType}, using reflections. Consider opening a GitHub issue to help improving the generator", messageType);

            lock (CacheLock) {
                if (!ConversionCache.TryGetValue(messageType, out conversion)) {
                    conversion = CreateConversionFunction(messageType);

                    ConversionCache[messageType] = conversion;
                }
            }
        }

        return (IMessageConsumeContext)conversion!(context);
    }

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "This should not be used because all the conversions should be pre-generated")]
    static Conversion CreateConversionFunction(Type messageType) {
        var contextType   = typeof(MessageConsumeContext<>).MakeGenericType(messageType);
        var contextParam  = Expression.Parameter(typeof(IMessageConsumeContext), "context");
        var newExpression = Expression.New(contextType.GetConstructor([typeof(IMessageConsumeContext)])!, contextParam);

        return Expression.Lambda<Conversion>(newExpression, contextParam).Compile();
    }
}
