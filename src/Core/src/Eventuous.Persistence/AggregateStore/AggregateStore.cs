// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

using static Diagnostics.PersistenceEventSource;

public class AggregateStore : IAggregateStore {
    readonly AmendEvent               _amendEvent;
    readonly AggregateFactoryRegistry _factoryRegistry;
    readonly IEventReader             _eventReader;
    readonly IEventWriter             _eventWriter;

    /// <summary>
    /// Creates a new instance of the default aggregate store
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="writer"></param>
    /// <param name="amendEvent"></param>
    /// <param name="factoryRegistry"></param>
    public AggregateStore(
            IEventReader              reader,
            IEventWriter              writer,
            AmendEvent?               amendEvent      = null,
            AggregateFactoryRegistry? factoryRegistry = null
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
    public AggregateStore(IEventStore eventStore, AmendEvent? amendEvent = null, AggregateFactoryRegistry? factoryRegistry = null)
        : this(eventStore, eventStore, amendEvent, factoryRegistry) { }

    /// <inheritdoc/>
    public Task<AppendEventsResult> Store<T, TState>(StreamName streamName, T aggregate, CancellationToken cancellationToken)
        where T : Aggregate<TState> where TState : State<TState>, new() => _eventWriter.Store<T, TState>(streamName, aggregate, _amendEvent, cancellationToken);

    /// <inheritdoc/>
    public Task<T> Load<T, TState>(StreamName streamName, CancellationToken cancellationToken) where T : Aggregate<TState> where TState : State<TState>, new()
        => LoadInternal<T, TState>(streamName, true, cancellationToken);

    /// <inheritdoc/>
    public Task<T> LoadOrNew<T, TState>(StreamName streamName, CancellationToken cancellationToken)
        where T : Aggregate<TState> where TState : State<TState>, new() => LoadInternal<T, TState>(streamName, false, cancellationToken);

    async Task<T> LoadInternal<T, TState>(StreamName streamName, bool failIfNotFound, CancellationToken cancellationToken)
        where T : Aggregate<TState> where TState : State<TState>, new() {
        var aggregate = _factoryRegistry.CreateInstance<T, TState>();

        try {
            var events = await _eventReader.ReadStream(streamName, StreamReadPosition.Start, failIfNotFound, cancellationToken);
            aggregate.Load(events.Select(x => x.Payload));
        } catch (StreamNotFound) when (!failIfNotFound) {
            return aggregate;
        } catch (Exception e) {
            Log.UnableToLoadAggregate<T, TState>(streamName, e);

            throw e is StreamNotFound ? new AggregateNotFoundException<T, TState>(streamName, e) : e;
        }

        return aggregate;
    }
}
