// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions.Filters;

using Consumers;
using Context;

public class ConsumerFilter(IMessageConsumer<IMessageConsumeContext> consumer) : ConsumeFilter<IMessageConsumeContext> {
    protected override ValueTask Send(IMessageConsumeContext context, LinkedListNode<IConsumeFilter>? next) => consumer.Consume(context);
}
