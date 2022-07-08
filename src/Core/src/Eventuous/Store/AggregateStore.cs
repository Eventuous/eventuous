// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using static Eventuous.Diagnostics.EventuousEventSource;

namespace Eventuous;

public class AggregateStore : IAggregateStore {
    readonly Func<StreamEvent, StreamEvent> _amendEvent;
    readonly AggregateFactoryRegistry       _factoryRegistry;
    readonly IEventReader                   _eventReader;
    readonly IEventWriter                   _eventWriter;

    /// <summary>
    /// Creates a new instance of the default aggregate store
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="writer"></param>
    /// <param name="amendEvent"></param>
    /// <param name="factoryRegistry"></param>
    public AggregateStore(
        IEventReader                    reader,
        IEventWriter                    writer,
        Func<StreamEvent, StreamEvent>? amendEvent      = null,
        AggregateFactoryRegistry?       factoryRegistry = null
    ) {
        _amendEvent      = amendEvent      ?? (x => x);
        _factoryRegistry = factoryRegistry ?? AggregateFactoryRegistry.Instance;
        _eventReader     = Ensure.NotNull(reader);
        _eventWriter     = Ensure.NotNull(writer);
    }

    /// <summary>
    /// Creates a new instance of the default aggregate store
    /// </summary>
    /// <param name="eventStore">Event store implementation</param>
    /// <param name="amendEvent"></param>
    /// <param name="factoryRegistry"></param>
    public AggregateStore(
        IEventStore                     eventStore,
        Func<StreamEvent, StreamEvent>? amendEvent      = null,
        AggregateFactoryRegistry?       factoryRegistry = null
    ) : this(eventStore, eventStore, amendEvent, factoryRegistry) { }

    public Task<AppendEventsResult> Store<T>(
        StreamName        streamName,
        T                 aggregate,
        CancellationToken cancellationToken
    ) where T : Aggregate
        => _eventWriter.Store(streamName, aggregate, _amendEvent, cancellationToken);

    public Task<T> Load<T>(StreamName streamName, CancellationToken cancellationToken) where T : Aggregate
        => LoadInternal<T>(streamName, true, cancellationToken);

    public Task<T> LoadOrNew<T>(StreamName streamName, CancellationToken cancellationToken)
        where T : Aggregate
        => LoadInternal<T>(streamName, false, cancellationToken);

    async Task<T> LoadInternal<T>(StreamName streamName, bool failIfNotFound, CancellationToken cancellationToken)
        where T : Aggregate {
        var aggregate = _factoryRegistry.CreateInstance<T>();

        try {
            var events = await _eventReader.ReadStream(
                streamName,
                StreamReadPosition.Start,
                failIfNotFound,
                cancellationToken
            );

            aggregate.Load(events.Select(x => x.Payload));
        }
        catch (StreamNotFound) when (!failIfNotFound) {
            return aggregate;
        }
        catch (Exception e) {
            Log.UnableToLoadAggregate<T>(streamName, e);
            throw e is StreamNotFound ? new AggregateNotFoundException<T>(streamName, e) : e;
        }

        return aggregate;
    }
}
