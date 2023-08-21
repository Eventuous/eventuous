// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

using static Diagnostics.PersistenceEventSource;

public class AggregateStore<TReader>(
        IEventStore                     eventStore,
        TReader                         archiveReader,
        Func<StreamEvent, StreamEvent>? amendEvent      = null,
        AggregateFactoryRegistry?       factoryRegistry = null
    ) : IAggregateStore where TReader : class, IEventReader {
    readonly Func<StreamEvent, StreamEvent> _amendEvent      = amendEvent      ?? (x => x);
    readonly AggregateFactoryRegistry       _factoryRegistry = factoryRegistry ?? AggregateFactoryRegistry.Instance;

    public Task<AppendEventsResult> Store<T>(StreamName streamName, T aggregate, CancellationToken cancellationToken) where T : Aggregate
        => eventStore.Store(streamName, aggregate, _amendEvent, cancellationToken);

    public Task<T> Load<T>(StreamName streamName, CancellationToken cancellationToken) where T : Aggregate
        => LoadInternal<T>(streamName, true, cancellationToken);

    public Task<T> LoadOrNew<T>(StreamName streamName, CancellationToken cancellationToken) where T : Aggregate
        => LoadInternal<T>(streamName, false, cancellationToken);

    async Task<T> LoadInternal<T>(StreamName streamName, bool failIfNotFound, CancellationToken cancellationToken) where T : Aggregate {
        var aggregate = _factoryRegistry.CreateInstance<T>();

        var hotEvents = await LoadStreamEvents(eventStore, StreamReadPosition.Start).NoContext();

        var archivedEvents =
            hotEvents.Length == 0 || hotEvents[0].Position > 0
                ? await LoadStreamEvents(archiveReader, StreamReadPosition.Start).NoContext()
                : Enumerable.Empty<StreamEvent>();

        var streamEvents = hotEvents.Concat(archivedEvents).Distinct(Comparer).ToArray();

        if (streamEvents.Length == 0 && failIfNotFound) {
            throw new AggregateNotFoundException<T>(streamName, new StreamNotFound(streamName));
        }

        aggregate.Load(streamEvents.Select(x => x.Payload));

        return aggregate;

        async Task<StreamEvent[]> LoadStreamEvents(IEventReader reader, StreamReadPosition start) {
            try {
                return await reader.ReadStream(streamName, start, failIfNotFound, cancellationToken).NoContext();
            } catch (StreamNotFound) {
                return Array.Empty<StreamEvent>();
            } catch (Exception e) {
                Log.UnableToLoadAggregate<T>(streamName, e);

                throw;
            }
        }
    }

    static readonly StreamEventPositionComparer Comparer = new();

    class StreamEventPositionComparer : IEqualityComparer<StreamEvent> {
        public bool Equals(StreamEvent x, StreamEvent y)
            => x.Position == y.Position;

        public int GetHashCode(StreamEvent obj)
            => obj.Position.GetHashCode();
    }
}
