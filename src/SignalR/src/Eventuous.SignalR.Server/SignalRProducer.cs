// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.AspNetCore.SignalR;

namespace Eventuous.SignalR.Server;

/// <summary>
/// Producer that sends event envelopes to a specific SignalR client connection via the hub context.
/// </summary>
/// <typeparam name="THub">The SignalR hub type.</typeparam>
public class SignalRProducer<THub>(IHubContext<THub> hubContext)
    : BaseProducer<SignalRProduceOptions>(new() { MessagingSystem = "signalr" })
    where THub : Hub {
    protected override async Task ProduceMessages(
            StreamName                   stream,
            IEnumerable<ProducedMessage> messages,
            SignalRProduceOptions?       options,
            CancellationToken            cancellationToken = default
        ) {
        ArgumentNullException.ThrowIfNull(options);
        var client = hubContext.Clients.Client(options.ConnectionId);

        foreach (var msg in messages) {
            await client.SendAsync(SignalRSubscriptionMethods.StreamEvent, msg.Message, cancellationToken).NoContext();
        }
    }
}
