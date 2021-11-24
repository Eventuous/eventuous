namespace Eventuous.Diagnostics.Tracing; 

public static class Constants {
    public const string HandleCommand    = "handle-command";
    public const string EventStorePrefix = "eventstore";
    public const string StreamExists     = $"{EventStorePrefix}-exists";
    public const string AppendEvents     = $"{EventStorePrefix}-append";
    public const string ReadEvents       = $"{EventStorePrefix}-read";
    public const string TruncateStream   = $"{EventStorePrefix}-truncate";
    public const string DeleteStream     = $"{EventStorePrefix}-delete";

    public const string CommandTag = "command";
}