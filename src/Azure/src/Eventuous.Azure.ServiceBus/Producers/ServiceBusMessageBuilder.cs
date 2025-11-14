// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Producers;
using static Eventuous.Azure.ServiceBus.Shared.ServiceBusHelper;

namespace Eventuous.Azure.ServiceBus.Producers;

using Shared;

class ServiceBusMessageBuilder(
        IEventSerializer                serializer,
        string                          streamName,
        ServiceBusMessageAttributeNames attributes,
        ServiceBusProduceOptions?       options                = null,
        Action<string>?                 setActivityMessageType = null
    ) {
    /// <summary>
    /// Creates a <see cref="ServiceBusMessage"/> from the provided <see cref="ProducedMessage"/>.
    /// This method serializes the event, sets the necessary properties, and adds custom application properties
    /// based on the metadata and additional headers.
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    internal ServiceBusMessage CreateServiceBusMessage(ProducedMessage message) {
        var (messageType, contentType, payload) = serializer.SerializeEvent(message.Message);
        setActivityMessageType?.Invoke(messageType);

        var metadata = message.Metadata;

        var serviceBusMessage = new ServiceBusMessage(payload) {
            ContentType = contentType,
            MessageId = metadata?.GetValueOrDefault(attributes.MessageId, message.MessageId)?.ToString(),
            Subject = metadata?.GetValueOrDefault(attributes.Subject, options?.Subject)?.ToString(),
            TimeToLive = options?.TimeToLive ?? TimeSpan.MaxValue,
            CorrelationId = message.Metadata?.GetCorrelationId(),
            To = metadata?.GetValueOrDefault(attributes.To, options?.To)?.ToString(),
            ReplyTo = metadata?.GetValueOrDefault(attributes.ReplyTo, options?.ReplyTo)?.ToString()
        };

        var reservedAttributes = attributes.ReservedNames();

        foreach (var property in GetCustomApplicationProperties(message, messageType, reservedAttributes)) {
            serviceBusMessage.ApplicationProperties.Add(property);
        }

        return serviceBusMessage;
    }

    IEnumerable<KeyValuePair<string, object>> GetCustomApplicationProperties(ProducedMessage message, string messageType, HashSet<string> reservedAttributes)
        => (message.Metadata ?? [])
            .Concat(message.AdditionalHeaders ?? [])
            .Concat([new(attributes.MessageType, messageType), new(attributes.StreamName, streamName)])
            .Where(pair => !reservedAttributes.Contains(pair.Key))
            .Where(pair => IsSerialisableByServiceBus(pair.Value))
            .Select(pair => new KeyValuePair<string, object>(pair.Key, pair.Value!));
}