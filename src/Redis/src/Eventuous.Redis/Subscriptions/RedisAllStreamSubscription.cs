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
        var linkedEvents = await database.StreamReadAsync("_all", position.ToRedisValue(), 100);
        var persistentEvents = new List<PersistentEvent>();

        foreach(var linkEvent in linkedEvents) {
            var stream = linkEvent["stream"];
            var streamPosition = linkEvent["position"];

            var streamEvents = await database.StreamRangeAsync(new RedisKey(stream), streamPosition);
            var entry = streamEvents[0];

            DateTime date;
            System.DateTime.TryParse(entry["created"]!, out date);
            persistentEvents.Add(new PersistentEvent(
                Guid.Parse(entry["message_id"]!),
                entry["message_type"]!,
                entry.Id.ToLong(),
                entry.Id.ToLong(),
                entry["json_data"]!,
                entry["json_metadata"],
                date,
                stream!));
        }
        return persistentEvents.ToArray();
    }

    protected override EventPosition GetPositionFromContext(IMessageConsumeContext context) 
        => EventPosition.FromContext(context);

}

public record RedisAllStreamSubscriptionOptions : RedisSubscriptionBaseOptions;


