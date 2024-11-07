// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions.Consumers;

using Context;

public interface IMessageConsumer<in TContext> where TContext : class, IMessageConsumeContext {
    ValueTask Consume(TContext context);
}

public interface IMessageConsumer : IMessageConsumer<IMessageConsumeContext>;