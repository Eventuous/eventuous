// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.AspNetCore.SignalR;

namespace Eventuous.SignalR.Server;

/// <summary>
/// SignalR hub that handles stream subscription requests from clients.
/// Map this hub in your endpoint configuration to expose stream subscriptions over SignalR.
/// </summary>
public class SignalRSubscriptionHub(SubscriptionGateway<SignalRSubscriptionHub> gateway) : Hub {
    /// <inheritdoc cref="SubscriptionGateway{THub}.SubscribeAsync"/>
    public Task SubscribeToStream(string stream, ulong? fromPosition)
        => gateway.SubscribeAsync(Context.ConnectionId, stream, fromPosition, Context.ConnectionAborted);

    /// <inheritdoc cref="SubscriptionGateway{THub}.UnsubscribeAsync"/>
    public Task UnsubscribeFromStream(string stream)
        => gateway.UnsubscribeAsync(Context.ConnectionId, stream);

    /// <inheritdoc />
    public override Task OnDisconnectedAsync(Exception? exception)
        => gateway.RemoveConnectionAsync(Context.ConnectionId);
}
