// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

using static Diagnostics.PersistenceEventSource;

public class AggregateStore<TReader>(
        IEventStore               eventStore,
        TReader                   archiveReader,
        AmendEvent?               amendEvent      = null,
        AggregateFactoryRegistry? factoryRegistry = null
    ) : IAggregateStore where TReader : class, IEventReader {
    readonly AggregateFactoryRegistry _factoryRegistry = factoryRegistry ?? AggregateFactoryRegistry.Instance;

    /// <inheritdoc/>
    public Task<AppendEventsResult> Store<T, TState>(StreamName streamName, T aggregate, CancellationToken cancellationToken)
        where T : Aggregate<TState> where TState : State<TState>, new() => eventStore.Store<T, TState>(streamName, aggregate, amendEvent, cancellationToken);

    /// <inheritdoc/>
    public Task<T> Load<T, TState>(StreamName streamName, CancellationToken cancellationToken) where T : Aggregate<TState> where TState : State<TState>, new()
        => LoadInternal<T, TState>(streamName, true, cancellationToken);

    /// <inheritdoc/>
    public Task<T> LoadOrNew<T, TState>(StreamName streamName, CancellationToken cancellationToken)
        where T : Aggregate<TState> where TState : State<TState>, new() => LoadInternal<T, TState>(streamName, false, cancellationToken);

    async Task<T> LoadInternal<T, TState>(StreamName streamName, bool failIfNotFound, CancellationToken cancellationToken)
        where T : Aggregate<TState> where TState : State<TState>, new() {
        var aggregate = _factoryRegistry.CreateInstance<T, TState>();

        var hotEvents = await LoadStreamEvents(eventStore, StreamReadPosition.Start).NoContext();

        var archivedEvents =
            hotEvents.Length == 0 || hotEvents[0].Position > 0
                ? await LoadStreamEvents(archiveReader, StreamReadPosition.Start).NoContext()
                : Enumerable.Empty<StreamEvent>();

        var streamEvents = hotEvents.Concat(archivedEvents).Distinct(Comparer).ToArray();

        if (streamEvents.Length == 0 && failIfNotFound) {
            throw new AggregateNotFoundException<T, TState>(streamName, new StreamNotFound(streamName));
        }

        aggregate.Load(streamEvents.Select(x => x.Payload));

        return aggregate;

        async Task<StreamEvent[]> LoadStreamEvents(IEventReader reader, StreamReadPosition start) {
            try {
                return await reader.ReadStream(streamName, start, failIfNotFound, cancellationToken).NoContext();
            } catch (StreamNotFound) {
                return [];
            } catch (Exception e) {
                Log.UnableToLoadAggregate<T, TState>(streamName, e);

                throw;
            }
        }
    }

    static readonly StreamEventPositionComparer Comparer = new();

    class StreamEventPositionComparer : IEqualityComparer<StreamEvent> {
        public bool Equals(StreamEvent x, StreamEvent y) => x.Position == y.Position;

        public int GetHashCode(StreamEvent obj) => obj.Position.GetHashCode();
    }
}
