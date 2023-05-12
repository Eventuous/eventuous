// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

using Microsoft.Extensions.Caching.Memory;
using static Diagnostics.PersistenceEventSource;

public class AggregateStore : IAggregateStore {
    readonly Func<StreamEvent, StreamEvent> _amendEvent;
    readonly AggregateFactoryRegistry       _factoryRegistry;
    readonly IEventReader                   _eventReader;
    readonly IEventWriter                   _eventWriter;
    readonly IMemoryCache?                  _memoryCache;

    /// <summary>
    /// Creates a new instance of the default aggregate store
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="writer"></param>
    /// <param name="amendEvent"></param>
    /// <param name="factoryRegistry"></param>
    /// <param name="memoryCache"></param>
    public AggregateStore(
        IEventReader                    reader,
        IEventWriter                    writer,
        Func<StreamEvent, StreamEvent>? amendEvent      = null,
        AggregateFactoryRegistry?       factoryRegistry = null,
        IMemoryCache?                   memoryCache     = null
    ) {
        _amendEvent      = amendEvent      ?? (x => x);
        _factoryRegistry = factoryRegistry ?? AggregateFactoryRegistry.Instance;
        _eventReader     = Ensure.NotNull(reader);
        _eventWriter     = Ensure.NotNull(writer);
        _memoryCache     = memoryCache;
    }

    /// <summary>
    /// Creates a new instance of the default aggregate store
    /// </summary>
    /// <param name="eventStore">Event store implementation</param>
    /// <param name="amendEvent"></param>
    /// <param name="factoryRegistry"></param>
    /// <param name="memoryCache"></param>
    public AggregateStore(
        IEventStore                     eventStore,
        Func<StreamEvent, StreamEvent>? amendEvent      = null,
        AggregateFactoryRegistry?       factoryRegistry = null,
        IMemoryCache?                   memoryCache     = null
    ) : this(eventStore, eventStore, amendEvent, factoryRegistry, memoryCache) { }

    public Task<AppendEventsResult> Store<T>(StreamName streamName, T aggregate, CancellationToken cancellationToken) where T : Aggregate {
        var result = _eventWriter.Store(streamName, aggregate, _amendEvent, cancellationToken);
        _memoryCache?.Set(streamName, aggregate.CreateSnapshot());
        return result;
    }

    public Task<T> Load<T>(StreamName streamName, CancellationToken cancellationToken) where T : Aggregate
        => LoadInternal<T>(streamName, true, cancellationToken);

    public Task<T> LoadOrNew<T>(StreamName streamName, CancellationToken cancellationToken) where T : Aggregate
        => LoadInternal<T>(streamName, false, cancellationToken);

    async Task<T> LoadInternal<T>(StreamName streamName, bool failIfNotFound, CancellationToken cancellationToken) where T : Aggregate {

        var aggregate = _factoryRegistry.CreateInstance<T>();

        if (_memoryCache != null && _memoryCache.TryGetValue(streamName, out Aggregate.Snapshot? snapshot))
            aggregate.Load(snapshot!);

        try {
            var events = await _eventReader.ReadStream(streamName, new(aggregate.CurrentVersion + 1), failIfNotFound, cancellationToken);
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
