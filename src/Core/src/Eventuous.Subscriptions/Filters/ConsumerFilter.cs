// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace Eventuous.Subscriptions.Filters;

using Consumers;
using Context;

public class ConsumerFilter(IMessageConsumer<IMessageConsumeContext> consumer) : ConsumeFilter<IMessageConsumeContext> {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override ValueTask Send(IMessageConsumeContext context, LinkedListNode<IConsumeFilter>? next) => consumer.Consume(context);
}
