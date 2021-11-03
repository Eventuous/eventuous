namespace Eventuous.Subscriptions.Context; 

public class ContextItems {
    readonly Dictionary<string, object?> _items = new();
    
    public void AddItem(string key, object? value) => _items.TryAdd(key, value);

    public T? TryGetItem<T>(string key)
        => _items.TryGetValue(key, out var value) && value is T val
            ? val
            : default;
}