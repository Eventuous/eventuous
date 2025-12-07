// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

/// <summary>
/// Represents a snapshot event with its metadata
/// </summary>
public record Snapshot {
    /// <summary>
    /// Stream revision at the time of snapshot creation
    /// </summary>
    public long Revision { get; init; }

    /// <summary>
    /// The snapshot event payload
    /// </summary>
    public object? Payload { get; init; }
}

