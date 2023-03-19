using System.Linq;
using System.Text;
using Eventuous;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Eventuous.Redis.Extension;

namespace Eventuous.Redis.Subscriptions;

public class RedisStreamSubscription : RedisSubscriptionBase<RedisSubscriptionBaseOptions> {

    public RedisStreamSubscription(
        GetRedisDatabase                    getDatabase,
        RedisStreamSubscriptionOptions      options,
        ICheckpointStore                    checkpointStore,
        ConsumePipe                         consumePipe,
        ILoggerFactory?                     loggerFactory
    ) : base (getDatabase, options, checkpointStore, consumePipe, loggerFactory) {
        _streamName = options.Stream.ToString();
    }

    protected override async Task BeforeSubscribe(CancellationToken cancellationToken) {
        var info     = await GetDatabase().StreamInfoAsync(_streamName);
        if (info.Length <= 0)
            throw new StreamNotFound(_streamName);
    }

    protected override async Task<PersistentEvent[]> ReadEvents(IDatabase database, long position)
    {
        var evts = await database.StreamReadAsync(_streamName, position.ToRedisValue(), 100);
        return evts.Select( evt =>  {

            DateTime date;
            System.DateTime.TryParse(evt["created"]!, out date);
            return new PersistentEvent (
                Guid.Parse(evt["message_id"]!),
                evt["message_type"]!,
                evt.Id.ToLong(),
                evt.Id.ToLong(),
                evt["json_data"]!,
                evt["json_metadata"],
                date,
                _streamName);
        }
        ).ToArray();
    }

    readonly string _streamName;

    protected override EventPosition GetPositionFromContext(IMessageConsumeContext context) 
        => EventPosition.FromContext(context);

}

public record RedisStreamSubscriptionOptions(StreamName Stream) : RedisSubscriptionBaseOptions;


