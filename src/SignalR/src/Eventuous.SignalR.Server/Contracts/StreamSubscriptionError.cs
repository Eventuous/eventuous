// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.SignalR;

/// <summary>
/// Error notification sent to the client when a server-side subscription fails.
/// </summary>
public record StreamSubscriptionError {
    /// <summary>The stream that experienced the error.</summary>
    public required string Stream { get; init; }

    /// <summary>Human-readable error message.</summary>
    public required string Message { get; init; }
}
