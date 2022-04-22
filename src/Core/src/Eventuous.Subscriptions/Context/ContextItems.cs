namespace Eventuous.Subscriptions.Context;

/// <summary>
/// A bag to transmit the necessary arbitrary baggage through the context pipe
/// </summary>
public class ContextItems {
    readonly Dictionary<string, object?> _items = new();

    /// <summary>
    /// Adds an item to the context baggage
    /// </summary>
    /// <param name="key">Item key</param>
    /// <param name="value">Item instance</param>
    /// <returns></returns>
    public ContextItems AddItem(string key, object? value) {
        _items.TryAdd(key, value);
        return this;
    }

    /// <summary>
    /// Gets an item from the baggage using the item key. If the item with a given key is not found,
    /// or the item type doesn't match, it returns the default value for the item time.
    /// </summary>
    /// <param name="key">Item key</param>
    /// <typeparam name="T">Item type</typeparam>
    /// <returns></returns>
    public T? TryGetItem<T>(string key)
        => _items.TryGetValue(key, out var value) && value is T val
            ? val
            : default;
}

/// <summary>
/// Pre-defined keys for well-known context items
/// </summary>
public static class ContextKeys {
    public const string GlobalPosition = nameof(GlobalPosition);
    public const string StreamPosition = nameof(StreamPosition);
}