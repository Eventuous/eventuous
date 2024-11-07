// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

[Obsolete("Use IEventStore instead")]
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

    /// <inheritdoc/>
    [Obsolete("Use IEventWriter.StoreAggregate<TAggregate, TState> instead.")]
    public Task<AppendEventsResult> Store<TAggregate, TState>(StreamName streamName, TAggregate aggregate, CancellationToken cancellationToken)
        where TAggregate : Aggregate<TState> where TState : State<TState>, new()
        => _eventWriter.StoreAggregate<TAggregate, TState>(streamName, aggregate, _amendEvent, cancellationToken);

    /// <inheritdoc/>
    [Obsolete("Use IEventReader.LoadAggregate<TAggregate, TState> instead.")]
    public Task<T> Load<T, TState>(StreamName streamName, CancellationToken cancellationToken) where T : Aggregate<TState> where TState : State<TState>, new()
        => _eventReader.LoadAggregate<T, TState>(streamName, true, _factoryRegistry, cancellationToken);

    /// <inheritdoc/>
    [Obsolete("Use IEventReader.LoadAggregate<TAggregate, TState> instead.")]
    public Task<T> LoadOrNew<T, TState>(StreamName streamName, CancellationToken cancellationToken)
        where T : Aggregate<TState> where TState : State<TState>, new()
        => _eventReader.LoadAggregate<T, TState>(streamName, false, _factoryRegistry, cancellationToken);
}
