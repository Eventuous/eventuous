// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions.Diagnostics;

public delegate ValueTask<EndOfStream> GetSubscriptionEndOfStream(CancellationToken cancellationToken);

[PublicAPI]
public record struct EndOfStream(string SubscriptionId, ulong Position, DateTime Timestamp) {
    public static readonly EndOfStream Invalid = new("error", 0, DateTime.MinValue);
}