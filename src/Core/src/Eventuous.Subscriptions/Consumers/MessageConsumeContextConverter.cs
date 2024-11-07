// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Linq.Expressions;

namespace Eventuous.Subscriptions.Consumers;

using Context;

static class MessageConsumeContextConverter {
    static readonly Dictionary<Type, Func<IMessageConsumeContext, object>?> ConversionCache = new();

    static readonly object CacheLock = new();

    public static IMessageConsumeContext ConvertToGeneric(this IMessageConsumeContext context) {
        var messageType = context.Message!.GetType();

        // ReSharper disable once InconsistentlySynchronizedField
        if (!ConversionCache.TryGetValue(messageType, out var conversion)) {
            lock (CacheLock) {
                if (!ConversionCache.TryGetValue(messageType, out conversion)) {
                    conversion = CreateConversionFunction(messageType);

                    ConversionCache[messageType] = conversion;
                }
            }
        }

        return (IMessageConsumeContext)conversion!(context);
    }

    static Func<IMessageConsumeContext, object> CreateConversionFunction(Type messageType) {
        var contextType   = typeof(MessageConsumeContext<>).MakeGenericType(messageType);
        var contextParam  = Expression.Parameter(typeof(IMessageConsumeContext), "context");
        var newExpression = Expression.New(contextType.GetConstructor([typeof(IMessageConsumeContext)])!, contextParam);
        return Expression.Lambda<Func<IMessageConsumeContext, object>>(newExpression, contextParam).Compile();
    }
}
