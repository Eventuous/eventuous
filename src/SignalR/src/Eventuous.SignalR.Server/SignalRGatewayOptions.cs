// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Filters;

namespace Eventuous.SignalR.Server;

/// <summary>
/// Factory delegate that creates an event store subscription for the given stream.
/// </summary>
/// <param name="stream">The stream name to subscribe to.</param>
/// <param name="fromPosition">Optional starting position.</param>
/// <param name="pipe">The consume pipe to process events through.</param>
/// <param name="subscriptionId">A unique identifier for the subscription.</param>
/// <returns>A message subscription instance.</returns>
public delegate IMessageSubscription SubscriptionFactory(StreamName stream, ulong? fromPosition, ConsumePipe pipe, string subscriptionId);

/// <summary>
/// Configuration options for <see cref="SubscriptionGateway{THub}"/>.
/// </summary>
public class SignalRGatewayOptions {
    /// <summary>
    /// Factory used to create event store subscriptions for each client stream request.
    /// </summary>
    public required SubscriptionFactory SubscriptionFactory { get; set; }
}
