// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Persistence;

namespace Eventuous;

using static Diagnostics.ApplicationEventSource;

/// <summary>
/// Command service base class. A derived class should be scoped to handle commands for one aggregate type only.
/// </summary>
/// <typeparam name="TAggregate">The aggregate type</typeparam>
/// <typeparam name="TState">The aggregate state type</typeparam>
/// <typeparam name="TId">The aggregate identity type</typeparam>
// [PublicAPI]
public abstract partial class CommandService<TAggregate, TState, TId>(
        IEventReader?             reader,
        IEventWriter?             writer,
        AggregateFactoryRegistry? factoryRegistry = null,
        StreamNameMap?            streamNameMap   = null,
        ITypeMapper?              typeMap         = null,
        AmendEvent?               amendEvent      = null
    )
    : ICommandService<TAggregate, TState, TId>
    where TAggregate : Aggregate<TState>
    where TState : State<TState>, new()
    where TId : Id {
    protected CommandService(
            IEventStore?              store,
            AggregateFactoryRegistry? factoryRegistry = null,
            StreamNameMap?            streamNameMap   = null,
            ITypeMapper?              typeMap         = null,
            AmendEvent?               amendEvent      = null
        ) : this(store, store, factoryRegistry, streamNameMap, typeMap, amendEvent) { }

    [PublicAPI]
    protected IEventReader? Reader { get; } = reader;
    [PublicAPI]
    protected IEventWriter? Writer { get; } = writer;

    readonly HandlersMap<TAggregate, TState, TId> _handlers        = new();
    readonly AggregateFactoryRegistry             _factoryRegistry = factoryRegistry ?? AggregateFactoryRegistry.Instance;
    readonly StreamNameMap                        _streamNameMap   = streamNameMap   ?? new StreamNameMap();
    readonly ITypeMapper                          _typeMap         = typeMap         ?? TypeMap.Instance;

    /// <summary>
    /// Returns the command handler builder for the specified command type.
    /// </summary>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <returns></returns>
    protected IDefineExpectedState<TCommand, TAggregate, TState, TId> On<TCommand>() where TCommand : class
        => new CommandHandlerBuilder<TCommand, TAggregate, TState, TId>(this, Reader, Writer);

    /// <summary>
    /// The command handler. Call this function from your edge (API).
    /// </summary>
    /// <param name="command">Command to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns><see cref="Result{TState}"/> of the execution</returns>
    /// <exception cref="Exceptions.CommandHandlerNotFound{TCommand}"></exception>
    public async Task<Result<TState>> Handle<TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : class {
        if (!_handlers.TryGet<TCommand>(out var registeredHandler)) {
            Log.CommandHandlerNotFound<TCommand>();
            var exception = new Exceptions.CommandHandlerNotFound(command.GetType());

            return Result<TState>.FromError(exception);
        }

        var aggregateId = await registeredHandler.GetId(command, cancellationToken).NoContext();
        var reader      = registeredHandler.ResolveReader(command);
        var stream      = _streamNameMap.GetStreamName<TAggregate, TState, TId>(aggregateId);

        try {
            var aggregate = registeredHandler.ExpectedState switch {
                ExpectedState.Any => await reader
                    .LoadAggregate<TAggregate, TState, TId>(aggregateId, _streamNameMap, false, _factoryRegistry, cancellationToken)
                    .NoContext(),
                ExpectedState.Existing => await reader
                    .LoadAggregate<TAggregate, TState, TId>(aggregateId, _streamNameMap, true, _factoryRegistry, cancellationToken)
                    .NoContext(),
                ExpectedState.New     => Create(aggregateId),
                ExpectedState.Unknown => default,
                _                     => throw new ArgumentOutOfRangeException(nameof(registeredHandler.ExpectedState), "Unknown expected state")
            };

            var result = await registeredHandler.Handler(aggregate!, command, cancellationToken).NoContext();

            // Zero in the global position would mean nothing, so the receiver needs to check the Changes.Length
            if (result.Changes.Count == 0) return Result<TState>.FromSuccess(result.State, Array.Empty<Change>(), 0);

            var proposed    = new ProposedAppend(stream, new(result.OriginalVersion), result.Changes.Select(x => new ProposedEvent(x, new())).ToArray());
            var final       = registeredHandler.AmendAppend?.Invoke(proposed, command) ?? proposed;
            var writer      = registeredHandler.ResolveWriter(command);
            var storeResult = await writer.Store(final, Amend, cancellationToken).NoContext();
            var changes     = result.Changes.Select(x => Change.FromEvent(x, _typeMap));
            Log.CommandHandled<TCommand>();

            return Result<TState>.FromSuccess(result.State, changes, storeResult.GlobalPosition);
        } catch (Exception e) {
            Log.ErrorHandlingCommand<TCommand>(e);

            return Result<TState>.FromError(e, $"Error handling command {typeof(TCommand).Name}");
        }

        TAggregate Create(TId id) => _factoryRegistry.CreateInstance<TAggregate, TState>().WithId<TAggregate, TState, TId>(id);

        NewStreamEvent Amend(NewStreamEvent streamEvent) {
            var evt = registeredHandler.AmendEvent?.Invoke(streamEvent, command) ?? streamEvent;

            return amendEvent?.Invoke(evt) ?? evt;
        }
    }

    internal void AddHandler<TCommand>(RegisteredHandler<TAggregate, TState, TId> handler) where TCommand : class
        => _handlers.AddHandlerUntyped(typeof(TCommand), handler);
}
