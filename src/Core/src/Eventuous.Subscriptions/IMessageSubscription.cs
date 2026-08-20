// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions;

using Diagnostics;

public delegate void OnSubscribed(string subscriptionId);

public delegate void OnDropped(string subscriptionId, DropReason dropReason, Exception? exception);

public delegate void OnUnsubscribed(string subscriptionId);

public interface IMessageSubscription {
    string SubscriptionId { get; }

    /// <summary>
    /// Starts the subscription, returning once it is up. One run per instance — calling it again before
    /// <see cref="Unsubscribe"/> completes throws <see cref="InvalidOperationException"/>. A subscription
    /// that failed to come up isn't running, so it can be started again.
    /// </summary>
    /// <param name="onSubscribed">Called each time the subscription comes up, including after a resubscribe.</param>
    /// <param name="onDropped">Called each time it goes down.</param>
    /// <param name="cancellationToken">Cancelling it stops the subscription.</param>
    ValueTask Subscribe(OnSubscribed onSubscribed, OnDropped onDropped, CancellationToken cancellationToken);

    ValueTask Unsubscribe(OnUnsubscribed onUnsubscribed, CancellationToken cancellationToken);
}

public interface IMeasuredSubscription {
    GetSubscriptionEndOfStream GetMeasure();
}
