using System.Linq;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Redis;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Eventuous.Redis.Extension;

namespace Eventuous.Redis.Subscriptions;

public class RedisAllStreamSubscription : RedisSubscriptionBase<RedisSubscriptionBaseOptions> {

    public RedisAllStreamSubscription(
        GetRedisDatabase                    getDatabase,
        RedisAllStreamSubscriptionOptions   options,
        ICheckpointStore                    checkpointStore,
        ConsumePipe                         consumePipe,
        ILoggerFactory?                     loggerFactory
    ) : base (getDatabase, options, checkpointStore, consumePipe, loggerFactory) {

    }

    protected override async Task<PersistentEvent[]> ReadEvents(IDatabase database, long position)
    {
        var allEvents = await database.StreamReadAsync("_all", (RedisValue)position, 100);

        var positions = allEvents.Select(allEvent => new StreamPosition(
            new RedisKey(allEvent["stream"]), allEvent["position"]
        )).ToArray();
        
        var streams = await database.StreamReadAsync(positions, 1);

        return streams.Select( stream => new PersistentEvent(
            Guid.Parse(stream.Entries[0]["message_id"]!),
            stream.Entries[0]["message_type"]!,
            stream.Entries[0].Id.ToLong(),
            stream.Entries[0].Id.ToLong(),
            stream.Entries[0]["json_data"]!,
            stream.Entries[0]["json_metadata"],
            System.DateTime.Parse(stream.Entries[0]["created"]!),
            stream.Key!
        )).ToArray();
    }

    protected override EventPosition GetPositionFromContext(IMessageConsumeContext context) 
        => EventPosition.FromContext(context);

}

public record RedisAllStreamSubscriptionOptions : RedisSubscriptionBaseOptions;


