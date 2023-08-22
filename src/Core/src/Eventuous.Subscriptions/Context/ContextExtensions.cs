// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace Eventuous.Subscriptions.Context;

public static class ContextExtensions {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? GetContext<T>(this IMessageConsumeContext ctx) where T : class, IMessageConsumeContext {
        while (true) {
            if (typeof(T) == ctx.GetType()) return (T)ctx;

            if (ctx is WrappedConsumeContext wrapped) {
                ctx = wrapped.InnerContext;
                continue;
            }

            return null;
        }
    }
}
