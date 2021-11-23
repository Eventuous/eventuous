namespace Eventuous.Subscriptions.Context;

public class ContextItems {
    readonly Dictionary<string, object?> _items = new();

    public ContextItems AddItem(string key, object? value) {
        _items.TryAdd(key, value);
        return this;
    }

    public T? TryGetItem<T>(string key)
        => _items.TryGetValue(key, out var value) && value is T val
            ? val
            : default;
}

public static class ContextKeys {
    public const string GlobalPosition = nameof(GlobalPosition);
    public const string StreamPosition = nameof(StreamPosition);
}