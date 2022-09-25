// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Consumers;
using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Filters;

public class ConsumerFilter : ConsumeFilter {
    readonly IMessageConsumer<IMessageConsumeContext> _consumer;

    public ConsumerFilter(IMessageConsumer<IMessageConsumeContext> consumer) => _consumer = consumer;

    public override ValueTask Send(IMessageConsumeContext context, Func<IMessageConsumeContext, ValueTask>? next)
        => _consumer.Consume(context);
}