// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Confluent.Kafka;
using Eventuous.Subscriptions;

namespace Eventuous.Kafka.Subscriptions;

public record KafkaSubscriptionOptions : SubscriptionOptions {
    public ConsumerConfig ConsumerConfig { get; init; } = null!;
}
