// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

[PublicAPI]
public class Metadata : Dictionary<string, object?> {
    public Metadata() { }

    public static Metadata FromMeta(Metadata? metadata) => metadata == null ? new Metadata() : new(metadata);

    public Metadata(IDictionary<string, object?> dictionary) : base(dictionary) { }

    public static Metadata FromHeaders(Dictionary<string, string?>? headers)
        => headers == null ? new Metadata() : new(headers.ToDictionary(x => x.Key, x => (object?)x.Value));

    public Dictionary<string, string?> ToHeaders() => this.ToDictionary(x => x.Key, x => x.Value?.ToString());

    public Metadata With<T>(string key, T? value) {
        if (value != null) this[key] = value;
        return this;
    }

    public string? GetString(string key) => TryGetValue(key, out var value) ? value?.ToString() : default;

    public T? Get<T>(string key) => TryGetValue(key, out var value) && value is T v ? v : default;

    public Metadata AddNotNull(string key, string? value) {
        if (!string.IsNullOrEmpty(value)) this[key] = value;
        return this;
    }
}
