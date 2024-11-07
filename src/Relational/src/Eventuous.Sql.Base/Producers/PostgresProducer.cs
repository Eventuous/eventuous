// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Producers;

namespace Eventuous.Sql.Base.Producers;

/// <summary>
/// Universal producer that uses the event store to append events to a stream
/// </summary>
/// <param name="store"></param>
public class UniversalProducer(IEventStore store) : IProducer {
    /// <inheritdoc />
    public async Task Produce(StreamName stream, IEnumerable<ProducedMessage> messages, CancellationToken cancellationToken = default) {
        var events = messages.Select(ToStreamEvent).ToList();
        await store.AppendEvents(stream, ExpectedStreamVersion.Any, events, cancellationToken);

        return;

        NewStreamEvent ToStreamEvent(ProducedMessage message) => new(Guid.NewGuid(), message.Message, message.Metadata ?? new Metadata());
    }
}
