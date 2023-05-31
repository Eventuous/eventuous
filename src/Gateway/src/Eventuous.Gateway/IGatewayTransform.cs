// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Context;

namespace Eventuous.Gateway;

/// <summary>
/// Interface for routing and transformation of messages.
/// </summary>
public interface IGatewayTransform {
    ValueTask<GatewayMessage[]> RouteAndTransform(IMessageConsumeContext context);
}

/// <summary>
/// Interface for routing and transformation of messages with produce options.
/// </summary>
public interface IGatewayTransform<TProduceOptions> {
    ValueTask<GatewayMessage<TProduceOptions>[]> RouteAndTransform(IMessageConsumeContext context);
}
