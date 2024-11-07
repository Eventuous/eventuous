// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

[Obsolete("Use TieredEventStore instead")]
public class AggregateStore<TReader>(
        IEventStore               eventStore,
        TReader                   archiveReader,
        AmendEvent?               amendEvent      = null,
        AggregateFactoryRegistry? factoryRegistry = null
    ) : IAggregateStore where TReader : class, IEventReader {
    readonly AggregateFactoryRegistry _factoryRegistry  = factoryRegistry ?? AggregateFactoryRegistry.Instance;
    readonly TieredEventStore         _tieredEventStore = new(eventStore, archiveReader);

    /// <inheritdoc/>
    public Task<AppendEventsResult> Store<TAggregate, TState>(StreamName streamName, TAggregate aggregate, CancellationToken cancellationToken)
        where TAggregate : Aggregate<TState> where TState : State<TState>, new()
        => eventStore.StoreAggregate<TAggregate, TState>(streamName, aggregate, amendEvent, cancellationToken);

    /// <inheritdoc/>
    public Task<TAggregate> Load<TAggregate, TState>(StreamName streamName, CancellationToken cancellationToken)
        where TAggregate : Aggregate<TState> where TState : State<TState>, new()
        => _tieredEventStore.LoadAggregate<TAggregate, TState>(streamName, true, _factoryRegistry, cancellationToken);

    /// <inheritdoc/>
    public Task<TAggregate> LoadOrNew<TAggregate, TState>(StreamName streamName, CancellationToken cancellationToken)
        where TAggregate : Aggregate<TState> where TState : State<TState>, new() 
        => _tieredEventStore.LoadAggregate<TAggregate, TState>(streamName, false, _factoryRegistry, cancellationToken);
}
