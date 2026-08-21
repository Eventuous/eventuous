// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;

namespace Eventuous.ElasticSearch.Store;

public class ElasticSerializer(IElasticsearchSerializer builtIn, JsonSerializerOptions? options, ITypeMapper? typeMapper = null)
    : IElasticsearchSerializer {
    readonly JsonSerializerOptions _options    = options    ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
    readonly ITypeMapper           _typeMapper = typeMapper ?? TypeMap.Instance;

    public object Deserialize(Type type, Stream stream) {
        // Read the stream directly: a BinaryReader here would either close the caller's stream when
        // disposed, or leak its buffers when not, and it only added a full copy of the payload
        var obj = JsonSerializer.Deserialize(stream, type, _options);

        if (type != typeof(PersistedEvent)) return obj!;

        var evt         = (PersistedEvent)obj!;
        var messageType = _typeMapper.GetType(evt.MessageType);
        var element     = (JsonElement)evt.Message!;
        var payload     = JsonSerializer.Deserialize(element.GetRawText(), messageType, _options);

        return evt with { Message = payload };
    }

    public void Serialize<T>(T data, Stream stream, SerializationFormatting formatting = SerializationFormatting.None) {
        if (data is not PersistedEvent) {
            builtIn.Serialize(data, stream, formatting);

            return;
        }

        // Disposing the writer returns its pooled buffers and flushes; it doesn't close the caller's stream
        using var writer = new Utf8JsonWriter(stream);
        JsonSerializer.Serialize(writer, data, _options);
    }

    public T Deserialize<T>(Stream stream) => (T)Deserialize(typeof(T), stream);

    public Task<object> DeserializeAsync(Type type, Stream stream, CancellationToken cancellationToken = default)
        => Task.FromResult(Deserialize(type, stream));

    public Task<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
        => Task.FromResult(Deserialize<T>(stream));

    public Task SerializeAsync<T>(
            T                       data,
            Stream                  stream,
            SerializationFormatting formatting        = SerializationFormatting.None,
            CancellationToken       cancellationToken = default
        ) {
        Serialize(data, stream, formatting);

        return Task.CompletedTask;
    }
}
