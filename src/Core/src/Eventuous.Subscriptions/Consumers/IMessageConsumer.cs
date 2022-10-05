// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Consumers; 

public interface IMessageConsumer<in TContext> where TContext : class, IMessageConsumeContext {
    ValueTask Consume(TContext context);
}

public interface IMessageConsumer : IMessageConsumer<IMessageConsumeContext> { }