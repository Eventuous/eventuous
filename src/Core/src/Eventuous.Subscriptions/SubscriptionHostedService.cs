// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions;

using Diagnostics;

// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class SubscriptionHostedService(
        IMessageSubscription subscription,
        ISubscriptionHealth? subscriptionHealth = null,
        ILoggerFactory?      loggerFactory      = null
    )
    : IHostedService {
    readonly CancellationTokenSource _subscriptionCts = new();

    ILogger<SubscriptionHostedService>? Log { get; } = loggerFactory?.CreateLogger<SubscriptionHostedService>();

    public virtual async Task StartAsync(CancellationToken cancellationToken) {
        Log?.LogDebug("Starting subscription {SubscriptionId}", subscription.SubscriptionId);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _subscriptionCts.Token);

        await subscription.Subscribe(
                id => subscriptionHealth?.ReportHealthy(id),
                (id, _, ex) => subscriptionHealth?.ReportUnhealthy(id, ex),
                cts.Token
            )
            .NoContext();

        Log?.LogInformation("Started subscription {SubscriptionId}", subscription.SubscriptionId);
    }

    public virtual async Task StopAsync(CancellationToken cancellationToken) {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _subscriptionCts.Token);

        await subscription.Unsubscribe(_ => { }, cts.Token).NoContext();
        Log?.LogInformation("Stopped subscription {SubscriptionId}", subscription.SubscriptionId);
    }
}
