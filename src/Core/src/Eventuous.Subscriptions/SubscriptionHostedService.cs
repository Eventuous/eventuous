// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions;

// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class SubscriptionHostedService : IHostedService {
    readonly CancellationTokenSource _subscriptionCts = new();
    readonly IMessageSubscription    _subscription;
    readonly ISubscriptionHealth?    _subscriptionHealth;

    public SubscriptionHostedService(
        IMessageSubscription subscription,
        ISubscriptionHealth? subscriptionHealth = null,
        ILoggerFactory?      loggerFactory      = null
    ) {
        _subscription       = subscription;
        _subscriptionHealth = subscriptionHealth;

        Log = loggerFactory?.CreateLogger<SubscriptionHostedService>();
    }

    ILogger<SubscriptionHostedService>? Log { get; }

    public virtual async Task StartAsync(CancellationToken cancellationToken) {
        Log?.LogDebug("Starting subscription {SubscriptionId}", _subscription.SubscriptionId);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _subscriptionCts.Token
        );

        await _subscription.Subscribe(
            id => _subscriptionHealth?.ReportHealthy(id),
            (id, _, ex) => _subscriptionHealth?.ReportUnhealthy(id, ex),
            cts.Token
        ).NoContext();

        Log?.LogInformation("Started subscription {SubscriptionId}", _subscription.SubscriptionId);
    }

    public virtual async Task StopAsync(CancellationToken cancellationToken) {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _subscriptionCts.Token
        );

        await _subscription.Unsubscribe(_ => { }, cts.Token).NoContext();
        Log?.LogInformation("Stopped subscription {SubscriptionId}", _subscription.SubscriptionId);
    }
}