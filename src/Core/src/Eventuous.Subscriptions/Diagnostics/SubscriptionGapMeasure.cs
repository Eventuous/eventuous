// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;

namespace Eventuous.Subscriptions.Diagnostics;

public delegate ValueTask<EndOfStream> GetSubscriptionEndOfStream(CancellationToken cancellationToken);

[PublicAPI]
[StructLayout(LayoutKind.Auto)]
public readonly record struct EndOfStream(string SubscriptionId, ulong Position, DateTime Timestamp) {
    public static readonly EndOfStream Invalid = new("error", 0, DateTime.MinValue);
}