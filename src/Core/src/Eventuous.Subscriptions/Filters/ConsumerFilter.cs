// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions.Filters;

using Consumers;
using Context;

public class ConsumerFilter : ConsumeFilter<IMessageConsumeContext> {
    readonly IMessageConsumer<IMessageConsumeContext> _consumer;

    public ConsumerFilter(IMessageConsumer<IMessageConsumeContext> consumer)
        => _consumer = consumer;

    protected override ValueTask Send(IMessageConsumeContext context, LinkedListNode<IConsumeFilter>? next)
        => _consumer.Consume(context);
}
