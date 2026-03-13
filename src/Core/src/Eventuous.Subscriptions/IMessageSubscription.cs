// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions;

using Diagnostics;

public delegate void OnSubscribed(string subscriptionId);

public delegate void OnDropped(string subscriptionId, DropReason dropReason, Exception? exception);

public delegate void OnUnsubscribed(string subscriptionId);

public interface IMessageSubscription {
    string SubscriptionId { get; }

    ValueTask Subscribe(OnSubscribed onSubscribed, OnDropped onDropped, CancellationToken cancellationToken);

    ValueTask Unsubscribe(OnUnsubscribed onUnsubscribed, CancellationToken cancellationToken);
}

public interface IMeasuredSubscription {
    GetSubscriptionEndOfStream GetMeasure();
}
