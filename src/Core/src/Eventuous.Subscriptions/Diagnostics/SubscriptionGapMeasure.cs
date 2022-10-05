// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions.Diagnostics;

public delegate ValueTask<SubscriptionGap> GetSubscriptionGap(CancellationToken cancellationToken);

[PublicAPI]
public record struct SubscriptionGap(string SubscriptionId, ulong PositionGap, TimeSpan TimeGap) {
    public static readonly SubscriptionGap Invalid = new("error", 0, TimeSpan.Zero);
}