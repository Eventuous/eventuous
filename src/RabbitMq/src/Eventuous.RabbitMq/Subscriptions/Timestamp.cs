// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.RabbitMq.Subscriptions; 

static class Timestamp {
    static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
    internal static AmqpTimestamp ToAmqpTimestamp(this DateTime datetime) {
        var unixTime = (datetime.ToUniversalTime() - Epoch).TotalSeconds;
        return new AmqpTimestamp((long) unixTime);
    }

    internal static DateTime ToDateTime(this AmqpTimestamp timestamp)
        => Epoch.AddSeconds(timestamp.UnixTime).ToLocalTime();
}