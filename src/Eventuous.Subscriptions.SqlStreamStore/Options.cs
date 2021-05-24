using SqlStreamStore;
using Eventuous;

namespace Eventuous.Subscriptions.SqlStreamStore {
    public abstract class SqlStreamStoreSubscriptionOptions : SubscriptionOptions {

    }

    public class StreamSubscriptionOptions : SqlStreamStoreSubscriptionOptions {
        public string StreamName { get; init; } = null!;
    }

    public class AllStreamSubscriptionOptions : SqlStreamStoreSubscriptionOptions {

    }
}