// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Filters;
using Microsoft.Extensions.Logging;

namespace Eventuous.Kafka.Subscriptions;

public class KafkaBasicSubscription(KafkaSubscriptionOptions options, ConsumePipe consumePipe, ILoggerFactory? loggerFactory)
    : EventSubscription<KafkaSubscriptionOptions>(options, consumePipe, loggerFactory) {
    protected override ValueTask Subscribe(CancellationToken cancellationToken)
        => throw new NotImplementedException();

    protected override ValueTask Unsubscribe(CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
