// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

using static Diagnostics.PersistenceEventSource;

public static class AggregatePersistenceExtensions {
    /// <summary>
    /// Store aggregate changes to the event store
    /// </summary>
    /// <param name="eventWriter">Event writer or event store</param>
    /// <param name="streamName">Stream name for the aggregate</param>
    /// <param name="aggregate">Aggregate instance</param>
    /// <param name="amendEvent">Optional: function to add extra information to the event before it gets stored</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="TAggregate">Aggregate type</typeparam>
    /// <typeparam name="TState">Aggregate state type</typeparam>
    /// <returns>Append event result</returns>
    /// <exception cref="OptimisticConcurrencyException{T, TState}">Gets thrown if the expected stream version mismatches with the given original stream version</exception>
    public static async Task<AppendEventsResult> StoreAggregate<TAggregate, TState>(
            this IEventWriter eventWriter,
            StreamName        streamName,
            TAggregate        aggregate,
            AmendEvent?       amendEvent        = null,
            CancellationToken cancellationToken = default
        ) where TAggregate : Aggregate<TState> where TState : State<TState>, new() {
        Ensure.NotNull(aggregate);

        try {
            return await eventWriter.Store(streamName, new(aggregate.OriginalVersion), aggregate.Changes, amendEvent, cancellationToken).NoContext();
        } catch (OptimisticConcurrencyException e) {
            Log.UnableToStoreAggregate<TAggregate, TState>(streamName, e);

            throw e.InnerException is null ? new OptimisticConcurrencyException<TAggregate, TState>(streamName, e) : new(streamName, e.InnerException);
        }
    }

    /// <summary>
    /// Store aggregate changes to the event store
    /// </summary>
    /// <param name="eventWriter">Event writer or event store</param>
    /// <param name="aggregate">Aggregate instance</param>
    /// <param name="id">Aggregate identity</param>
    /// <param name="streamNameMap">Optional: stream name map</param>
    /// <param name="amendEvent">Optional: function to add extra information to the event before it gets stored</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="TAggregate">Aggregate type</typeparam>
    /// <typeparam name="TState">Aggregate state type</typeparam>
    /// <typeparam name="TId">Aggregate identity type</typeparam>
    /// <returns>Append event result</returns>
    /// <exception cref="OptimisticConcurrencyException{T, TState}">Gets thrown if the expected stream version mismatches with the given original stream version</exception>
    public static Task<AppendEventsResult> StoreAggregate<TAggregate, TState, TId>(
            this IEventWriter eventWriter,
            TAggregate        aggregate,
            TId               id,
            StreamNameMap?    streamNameMap     = null,
            AmendEvent?       amendEvent        = null,
            CancellationToken cancellationToken = default
        ) where TAggregate : Aggregate<TState> where TState : State<TState>, new() where TId : Id {
        Ensure.NotNull(aggregate);

        if (aggregate.State is State<TState, TId> stateWithId && stateWithId.Id != id) {
            throw new InvalidOperationException($"Provided aggregate id {id} doesn't match an existing aggregate id {stateWithId.Id}");
        }

        var streamName = streamNameMap?.GetStreamName<TAggregate, TState, TId>(id) ?? StreamNameFactory.For<TAggregate, TState, TId>(id);

        return eventWriter.Store(streamName, new(aggregate.OriginalVersion), aggregate.Changes, amendEvent, cancellationToken);
    }

    /// <summary>
    /// Store aggregate changes to the event store
    /// </summary>
    /// <param name="eventWriter">Event writer or event store</param>
    /// <param name="aggregate">Aggregate instance</param>
    /// <param name="streamNameMap">Optional: stream name map</param>
    /// <param name="amendEvent">Optional: function to add extra information to the event before it gets stored</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="TAggregate">Aggregate type</typeparam>
    /// <typeparam name="TState">Aggregate state type</typeparam>
    /// <typeparam name="TId">Aggregate identity type</typeparam>
    /// <returns>Append event result</returns>
    /// <exception cref="OptimisticConcurrencyException{T, TState}">Gets thrown if the expected stream version mismatches with the given original stream version</exception>
    public static Task<AppendEventsResult> StoreAggregate<TAggregate, TState, TId>(
            this IEventWriter eventWriter,
            TAggregate        aggregate,
            StreamNameMap?    streamNameMap     = null,
            AmendEvent?       amendEvent        = null,
            CancellationToken cancellationToken = default
        ) where TAggregate : Aggregate<TState> where TState : State<TState, TId>, new() where TId : Id {
        Ensure.NotNull(aggregate);

        var streamName = streamNameMap?.GetStreamName<TAggregate, TState, TId>(aggregate.State.Id) ??
            StreamNameFactory.For<TAggregate, TState, TId>(aggregate.State.Id);

        return eventWriter.Store(streamName, new(aggregate.OriginalVersion), aggregate.Changes, amendEvent, cancellationToken);
    }

    /// <summary>
    /// Loads aggregate from event store
    /// </summary>
    /// <param name="eventReader">Event reader or store</param>
    /// <param name="streamName">Name of the aggregate stream</param>
    /// <param name="failIfNotFound">Either fail if the stream is not found, default is false</param>
    /// <param name="factoryRegistry">Optional: aggregate factory registry. Default instance will be used if the argument isn't provided.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="TAggregate">Aggregate type</typeparam>
    /// <typeparam name="TState">Aggregate state type</typeparam>
    /// <returns>Aggregate instance</returns>
    /// <exception cref="AggregateNotFoundException{T,TState}">If failIfNotFound set to true, this exception is thrown if there's no stream</exception>
    /// <exception cref="Exception"></exception>
    public static async Task<TAggregate> LoadAggregate<TAggregate, TState>(
            this IEventReader         eventReader,
            StreamName                streamName,
            bool                      failIfNotFound    = true,
            AggregateFactoryRegistry? factoryRegistry   = null,
            CancellationToken         cancellationToken = default
        )
        where TAggregate : Aggregate<TState> where TState : State<TState>, new() {
        var aggregate = (factoryRegistry ?? AggregateFactoryRegistry.Instance).CreateInstance<TAggregate, TState>();

        try {
            var events = await eventReader.ReadStream(streamName, StreamReadPosition.Start, failIfNotFound, cancellationToken).NoContext();
            aggregate.Load(events.Select(x => x.Payload));
        } catch (StreamNotFound) when (!failIfNotFound) {
            return aggregate;
        } catch (Exception e) {
            Log.UnableToLoadAggregate<TAggregate, TState>(streamName, e);

            throw e is StreamNotFound ? new AggregateNotFoundException<TAggregate, TState>(streamName, e) : e;
        }

        return aggregate;
    }

    /// <summary>
    /// Loads aggregate from event store
    /// </summary>
    /// <param name="eventReader">Event reader or store</param>
    /// <param name="aggregateId">Aggregate identity</param>
    /// <param name="streamNameMap">Optional: stream name map. Default instance is used when argument isn't provided.</param>
    /// <param name="failIfNotFound">Either fail if the stream is not found, default is false</param>
    /// <param name="factoryRegistry">Optional: aggregate factory registry. Default instance will be used if the argument isn't provided.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="TAggregate">Aggregate type</typeparam>
    /// <typeparam name="TState">Aggregate state type</typeparam>
    /// <typeparam name="TId">Aggregate identity type</typeparam>
    /// <returns>Aggregate instance</returns>
    /// <exception cref="AggregateNotFoundException{T,TState}">If failIfNotFound set to true, this exception is thrown if there's no stream</exception>
    /// <exception cref="Exception"></exception>
    public static async Task<TAggregate> LoadAggregate<TAggregate, TState, TId>(
            this IEventReader         eventReader,
            TId                       aggregateId,
            StreamNameMap?            streamNameMap     = null,
            bool                      failIfNotFound    = true,
            AggregateFactoryRegistry? factoryRegistry   = null,
            CancellationToken         cancellationToken = default
        )
        where TAggregate : Aggregate<TState> where TState : State<TState>, new() where TId : Id {
        var streamName = streamNameMap?.GetStreamName<TAggregate, TState, TId>(aggregateId)
         ?? StreamNameFactory.For<TAggregate, TState, TId>(aggregateId);
        var aggregate = await eventReader.LoadAggregate<TAggregate, TState>(streamName, failIfNotFound, factoryRegistry, cancellationToken).NoContext();

        return aggregate.WithId<TAggregate, TState, TId>(aggregateId);
    }
}
