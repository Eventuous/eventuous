namespace Eventuous.Diagnostics.Tracing; 

public static class Constants {
    public const string AppServicePrefix   = "service";
    public const string EventStorePrefix   = "eventstore";
    public const string SubscriptionPrefix = "sub";
    public const string ConsumerPrefix     = "consume";
    public const string EventHandlerPrefix = "handle";
    public const string StreamExists       = "exists";
    public const string AppendEvents       = "append";
    public const string ReadEvents         = "read";
    public const string TruncateStream     = "truncate";
    public const string DeleteStream       = "delete";

    public const string CommandTag = "command";
}