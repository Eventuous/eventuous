namespace Eventuous;

[PublicAPI]
public class Metadata : Dictionary<string, object> {
    public Metadata() { }

    public static Metadata FromMeta(Metadata? metadata) => metadata == null ? new Metadata() : new Metadata(metadata);

    public Metadata(IDictionary<string, object> dictionary) : base(dictionary) { }

    public Metadata With<T>(string key, T? value) {
        if (value != null) TryAdd(key, value);
        return this;
    }

    public string? GetString(string key) => TryGetValue(key, out var value) ? value.ToString() : default;
    
    public T? Get<T>(string key) => TryGetValue(key, out var value) && value is T v ? v : default;
}