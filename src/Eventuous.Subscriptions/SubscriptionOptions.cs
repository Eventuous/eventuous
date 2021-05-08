namespace Eventuous.Subscriptions {
    public class SubscriptionOptions {
        public string SubscriptionId { get; init; } = null!;
        
        public bool ThrowOnError { get; init; }
    }
}