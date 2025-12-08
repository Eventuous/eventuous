// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions;

namespace Eventuous.Kafka.Subscriptions;

/// <summary>
/// Options for Kafka subscription.
/// </summary>
[PublicAPI]
public record KafkaSubscriptionOptions : SubscriptionOptions {
    /// <summary>
    /// Confluent.Kafka consumer configuration.
    /// </summary>
    public ConsumerConfig ConsumerConfig { get; init; } = null!;

    /// <summary>
    /// Topic name to subscribe to.
    /// </summary>
    public string Topic { get; init; } = null!;

    /// <summary>
    /// Number of concurrent consumers, default is one.
    /// </summary>
    public uint ConcurrencyLimit { get; init; } = 1;

    /// <summary>
    /// A function to handle event processing failure. If not specified, the default handler will be used.
    /// </summary>
    public HandleConsumeError? FailureHandler { get; init; }
}

/// <summary>
/// Delegate for handling Kafka consume errors.
/// </summary>
/// <param name="consumer">The Kafka consumer</param>
/// <param name="result">The consume result containing the message</param>
/// <param name="exception">The exception that occurred during processing</param>
public delegate void HandleConsumeError(IConsumer<string, byte[]> consumer, ConsumeResult<string, byte[]> result, Exception? exception);
