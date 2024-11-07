// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;

namespace Eventuous.ElasticSearch.Store;

public class ElasticSerializer(IElasticsearchSerializer builtIn, JsonSerializerOptions? options, ITypeMapper? typeMapper = null)
    : IElasticsearchSerializer {
    readonly JsonSerializerOptions _options    = options    ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
    readonly ITypeMapper           _typeMapper = typeMapper ?? TypeMap.Instance;

    public object Deserialize(Type type, Stream stream) {
        var reader = new BinaryReader(stream);
        var obj    = JsonSerializer.Deserialize(reader.ReadBytes((int)stream.Length), type, _options);

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

        var writer = new Utf8JsonWriter(stream);
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
