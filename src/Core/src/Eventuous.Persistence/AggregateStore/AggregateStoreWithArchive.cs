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
    [RequiresDynamicCode(AttrConstants.DynamicSerializationMessage)]
    [RequiresUnreferencedCode(AttrConstants.DynamicSerializationMessage)]
    public Task<AppendEventsResult> Store<TAggregate, TState>(StreamName streamName, TAggregate aggregate, CancellationToken cancellationToken)
        where TAggregate : Aggregate<TState> where TState : State<TState>, new()
        => eventStore.StoreAggregate<TAggregate, TState>(streamName, aggregate, amendEvent, cancellationToken);

    /// <inheritdoc/>
    [RequiresDynamicCode(AttrConstants.DynamicSerializationMessage)]
    [RequiresUnreferencedCode(AttrConstants.DynamicSerializationMessage)]
    public Task<TAggregate> Load<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TAggregate, TState>(StreamName streamName, CancellationToken cancellationToken)
        where TAggregate : Aggregate<TState> where TState : State<TState>, new()
        => _tieredEventStore.LoadAggregate<TAggregate, TState>(streamName, true, _factoryRegistry, cancellationToken);

    /// <inheritdoc/>
    [RequiresDynamicCode(AttrConstants.DynamicSerializationMessage)]
    [RequiresUnreferencedCode(AttrConstants.DynamicSerializationMessage)]
    public Task<TAggregate> LoadOrNew<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TAggregate, TState>(StreamName streamName, CancellationToken cancellationToken)
        where TAggregate : Aggregate<TState> where TState : State<TState>, new()
        => _tieredEventStore.LoadAggregate<TAggregate, TState>(streamName, false, _factoryRegistry, cancellationToken);
}
