// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.KurrentDB.Producers; 

/// <summary>
/// Event producing options
/// </summary>
[PublicAPI]
public record KurrentDBProduceOptions {
    /// <summary>
    /// User credentials
    /// </summary>
    public UserCredentials? Credentials { get; init; }
        
    /// <summary>
    /// Expected stream state
    /// </summary>
    public StreamState ExpectedState { get; init; } = StreamState.Any;

    /// <summary>
    /// Maximum number of events appended to a single stream in one batch
    /// </summary>
    public int MaxAppendEventsCount { get; init; } = 500;

    /// <summary>
    /// Timeout for the produce operation
    /// </summary>
    public TimeSpan? Deadline { get; init; }

    /// <summary>
    /// Default set of options
    /// </summary>
    public static KurrentDBProduceOptions Default { get; } = new();
}