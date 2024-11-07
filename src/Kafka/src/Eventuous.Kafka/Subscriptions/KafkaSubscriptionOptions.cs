// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions;

namespace Eventuous.Kafka.Subscriptions;

public record KafkaSubscriptionOptions : SubscriptionOptions {
    public ConsumerConfig ConsumerConfig { get; init; } = null!;
}
