// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Filters;
using Microsoft.Extensions.Logging;
using static Eventuous.Redis.EventuousRedisKeys;

namespace Eventuous.Redis.Subscriptions;

using Tools;

public class RedisStreamSubscription(
        GetRedisDatabase               getDatabase,
        RedisStreamSubscriptionOptions options,
        ICheckpointStore               checkpointStore,
        ConsumePipe                    consumePipe,
        ILoggerFactory?                loggerFactory
    )
    : RedisSubscriptionBase<RedisSubscriptionBaseOptions>(getDatabase, options, checkpointStore, consumePipe, SubscriptionKind.Stream, loggerFactory) {
    protected override async Task BeforeSubscribe(CancellationToken cancellationToken) {
        var info = await GetDatabase().StreamInfoAsync(_streamName).NoContext();
        if (info.Length <= 0) throw new StreamNotFound(_streamName);
    }

    protected override async Task<ReceivedEvent[]> ReadEvents(IDatabase database, long position) {
        var events = await database.StreamReadAsync(_streamName, position.ToRedisValue(), Options.MaxPageSize).NoContext();

        return events.Select(
                evt => new ReceivedEvent(
                    Guid.Parse(evt[MessageId]!),
                    evt[MessageType]!,
                    evt.Id.ToLong(),
                    evt.Id.ToLong(),
                    evt[JsonData]!,
                    evt[JsonMetadata],
                    DateTime.Parse(evt[Created]!, CultureInfo.InvariantCulture),
                    _streamName
                )
            )
            .ToArray();
    }

    readonly string _streamName = options.Stream.ToString();
}

public record RedisStreamSubscriptionOptions(StreamName Stream) : RedisSubscriptionBaseOptions;
