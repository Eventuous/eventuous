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

public class RedisAllStreamSubscription(
        GetRedisDatabase                  getDatabase,
        RedisAllStreamSubscriptionOptions options,
        ICheckpointStore                  checkpointStore,
        ConsumePipe                       consumePipe,
        ILoggerFactory?                   loggerFactory
    )
    : RedisSubscriptionBase<RedisSubscriptionBaseOptions>(getDatabase, options, checkpointStore, consumePipe, SubscriptionKind.All, loggerFactory) {
    protected override async Task<ReceivedEvent[]> ReadEvents(IDatabase database, long position) {
        var linkedEvents     = await database.StreamReadAsync("_all", position.ToRedisValue(), Options.MaxPageSize).NoContext();
        var persistentEvents = new List<ReceivedEvent>();

        foreach (var linkEvent in linkedEvents) {
            var stream         = linkEvent[EventuousRedisKeys.Stream];
            var streamPosition = linkEvent[Position];

            var streamEvents = await database.StreamRangeAsync(new RedisKey(stream), streamPosition).NoContext();
            var entry        = streamEvents[0];

            persistentEvents.Add(
                new ReceivedEvent(
                    Guid.Parse(entry[MessageId]!),
                    entry[MessageType]!,
                    entry.Id.ToLong(),
                    entry.Id.ToLong(),
                    entry[JsonData]!,
                    entry[JsonMetadata],
                    DateTime.Parse(entry[Created]!, CultureInfo.InvariantCulture),
                    stream!
                )
            );
        }

        return persistentEvents.ToArray();
    }
}

public record RedisAllStreamSubscriptionOptions : RedisSubscriptionBaseOptions;
