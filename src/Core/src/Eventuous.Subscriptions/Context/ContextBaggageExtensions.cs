// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace Eventuous.Subscriptions.Context;

public static class ContextBaggageExtensions {
    /// <summary>
    /// Adds an arbitrary baggage item to the context
    /// </summary>
    /// <param name="ctx">Consume context</param>
    /// <param name="key">Item key</param>
    /// <param name="item">Item instance</param>
    /// <typeparam name="T">Type of the context instance</typeparam>
    /// <typeparam name="TItem">Type of the item</typeparam>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T WithItem<T, TItem>(this T ctx, string key, TItem item) where T : IMessageConsumeContext {
        ctx.Items.AddItem(key, item);
        return ctx;
    }
}