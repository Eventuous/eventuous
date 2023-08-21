// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace Eventuous.Subscriptions.Filters;

using Context;

public delegate bool FilterMessage(IMessageConsumeContext receivedEvent);

public class MessageFilter(FilterMessage filter) : ConsumeFilter<IMessageConsumeContext> {
    readonly FilterMessage _filter = Ensure.NotNull(filter);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override ValueTask Send(IMessageConsumeContext context, LinkedListNode<IConsumeFilter>? next) {
        if (next?.Value == null) return default;

        if (_filter(context)) return next.Value.Send(context, next.Next);

        context.Ignore<MessageFilter>();
        return default;
    }
}
