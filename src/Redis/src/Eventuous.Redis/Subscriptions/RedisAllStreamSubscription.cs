// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Redis.Tools;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Microsoft.Extensions.Logging;

namespace Eventuous.Redis.Subscriptions;

public class RedisAllStreamSubscription : RedisSubscriptionBase<RedisSubscriptionBaseOptions> {
    public RedisAllStreamSubscription(
        GetRedisDatabase                  getDatabase,
        RedisAllStreamSubscriptionOptions options,
        ICheckpointStore                  checkpointStore,
        ConsumePipe                       consumePipe,
        ILoggerFactory?                   loggerFactory
    ) : base(getDatabase, options, checkpointStore, consumePipe, loggerFactory) { }

    protected override async Task<ReceivedEvent[]> ReadEvents(IDatabase database, long position) {
        var linkedEvents     = await database.StreamReadAsync("_all", position.ToRedisValue(), Options.MaxPageSize);
        var persistentEvents = new List<ReceivedEvent>();

        foreach (var linkEvent in linkedEvents) {
            var stream         = linkEvent["stream"];
            var streamPosition = linkEvent["position"];

            var streamEvents = await database.StreamRangeAsync(new RedisKey(stream), streamPosition);
            var entry        = streamEvents[0];

            persistentEvents.Add(
                new ReceivedEvent(
                    Guid.Parse(entry["message_id"]!),
                    entry["message_type"]!,
                    entry.Id.ToLong(),
                    entry.Id.ToLong(),
                    entry["json_data"]!,
                    entry["json_metadata"],
                    DateTime.Parse(entry["created"]!),
                    stream!
                )
            );
        }

        return persistentEvents.ToArray();
    }

    protected override EventPosition GetPositionFromContext(IMessageConsumeContext context)
        => EventPosition.FromContext(context);
}

public record RedisAllStreamSubscriptionOptions : RedisSubscriptionBaseOptions;
