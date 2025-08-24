// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using Eventuous.Producers;

namespace Eventuous.Azure.ServiceBus.Producers;

using Shared;

class ServiceBusMessageBatchBuilder {
    readonly IEventSerializer                _serializer;
    readonly ServiceBusMessageAttributeNames _attributes;
    readonly Action<string>?                 _setActivityMessageType;
    readonly ServiceBusSender                _sender;

    internal ServiceBusMessageBatchBuilder(ServiceBusSender sender, IEventSerializer serializer, Shared.ServiceBusMessageAttributeNames attributes, Action<string>? setActivityMessageType) {
        this._sender                 = sender;
        this._serializer             = serializer;
        this._attributes             = attributes;
        this._setActivityMessageType = setActivityMessageType;
    }

    /// <summary>
    /// Creates a sequence of <see cref="ServiceBusMessageBatch"/> from the provided produced messages
    /// so we can optimise if we want to produce a large number of messages at once.
    /// This is useful for bulk operations where you want to send many messages in a single batch.
    /// We also return the produced messages so that we can track what was sent in each batch.
    /// </summary>
    /// <param name="messages"></param>
    /// <param name="stream"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    internal async IAsyncEnumerable<(ServiceBusMessageBatch, IList<ProducedMessage>)> CreateMessageBatches(
            IEnumerable<ProducedMessage>               messages,
            StreamName                                 stream,
            ServiceBusProduceOptions?                  options,
            [EnumeratorCancellation] CancellationToken cancellationToken = default
        ) {
        using var enumerator = messages.GetEnumerator();

        var messageBuilder = new ServiceBusMessageBuilder(_serializer, stream, _attributes, options, _setActivityMessageType);
        var notDone        = enumerator.MoveNext();

        while (notDone) {
            using var batch = await _sender.CreateMessageBatchAsync(cancellationToken);

            var produced = new List<ProducedMessage>();

            while (batch.TryAddMessage(messageBuilder.CreateServiceBusMessage(enumerator.Current))) {
                produced.Add(enumerator.Current);
                notDone = enumerator.MoveNext();

                if (!notDone) {
                    break;
                }
            }

            if (cancellationToken.IsCancellationRequested) {
                yield break;
            }

            yield return (batch, produced);
        }
    }
}
