// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

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
        IAggregateStore?          store,
        AggregateFactoryRegistry? factoryRegistry = null,
        StreamNameMap?            streamNameMap   = null,
        TypeMapper?               typeMap         = null
    )
    : ICommandService<TAggregate, TState, TId>, ICommandService<TAggregate>
    where TAggregate : Aggregate<TState>, new()
    where TState : State<TState>, new()
    where TId : Id {
    [PublicAPI]
    protected IAggregateStore? Store { get; } = store;

    readonly HandlersMap<TAggregate, TId> _handlers        = new();
    readonly AggregateFactoryRegistry     _factoryRegistry = factoryRegistry ?? AggregateFactoryRegistry.Instance;
    readonly StreamNameMap                _streamNameMap   = streamNameMap   ?? new StreamNameMap();
    readonly TypeMapper                   _typeMap         = typeMap         ?? TypeMap.Instance;

    bool _initialized;

    /// <summary>
    /// Returns the command handler builder for the specified command type.
    /// </summary>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <returns></returns>
    protected CommandHandlerBuilder<TCommand, TAggregate, TState, TId> On<TCommand>() where TCommand : class {
        var builder = new CommandHandlerBuilder<TCommand, TAggregate, TState, TId>(Store);
        _builders.Add(typeof(TCommand), builder);

        return builder;
    }

    /// <summary>
    /// The command handler. Call this function from your edge (API).
    /// </summary>
    /// <param name="command">Command to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns><see cref="Result{TState}"/> of the execution</returns>
    /// <exception cref="Exceptions.CommandHandlerNotFound{TCommand}"></exception>
    public async Task<Result<TState>> Handle<TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : class {
        if (!_initialized) BuildHandlers();

        if (!_handlers.TryGet<TCommand>(out var registeredHandler)) {
            Log.CommandHandlerNotFound<TCommand>();
            var exception = new Exceptions.CommandHandlerNotFound(command.GetType());

            return new ErrorResult<TState>(exception);
        }

        var aggregateId = await registeredHandler.GetId(command, cancellationToken).NoContext();
        var store       = registeredHandler.ResolveStore(command);

        try {
            var aggregate = registeredHandler.ExpectedState switch {
                ExpectedState.Any      => await store.LoadOrNew<TAggregate, TState, TId>(_streamNameMap, aggregateId, cancellationToken).NoContext(),
                ExpectedState.Existing => await store.Load<TAggregate, TState, TId>(_streamNameMap, aggregateId, cancellationToken).NoContext(),
                ExpectedState.New      => Create(aggregateId),
                ExpectedState.Unknown  => default,
                _                      => throw new ArgumentOutOfRangeException(nameof(registeredHandler.ExpectedState), "Unknown expected state")
            };

            var result = await registeredHandler
                .Handler(aggregate!, command, cancellationToken)
                .NoContext();

            // Zero in the global position would mean nothing, so the receiver need to check the Changes.Length
            if (result.Changes.Count == 0) return new OkResult<TState>(result.State, Array.Empty<Change>(), 0);

            var storeResult = await store.Store(GetAggregateStreamName(), result, cancellationToken).NoContext();
            var changes     = result.Changes.Select(x => new Change(x, _typeMap.GetTypeName(x)));
            Log.CommandHandled<TCommand>();

            return new OkResult<TState>(result.State, changes, storeResult.GlobalPosition);
        } catch (Exception e) {
            Log.ErrorHandlingCommand<TCommand>(e);

            return new ErrorResult<TState>($"Error handling command {typeof(TCommand).Name}", e);
        }

        TAggregate Create(TId id) => _factoryRegistry.CreateInstance<TAggregate>().WithId<TAggregate, TState, TId>(id);

        StreamName GetAggregateStreamName() => _streamNameMap.GetStreamName<TAggregate, TId>(aggregateId);
    }

    async Task<Result> ICommandService.Handle<TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : class {
        var result = await Handle(command, cancellationToken).NoContext();

        return result switch {
            OkResult<TState>(var state, var enumerable, _) => new OkResult(state, enumerable),
            ErrorResult<TState> error                      => new ErrorResult(error.Message, error.Exception),
            _                                              => throw new ApplicationException("Unknown result type")
        };
    }

    readonly Dictionary<Type, CommandHandlerBuilder<TAggregate, TState, TId>> _builders = new();

    [MethodImpl(MethodImplOptions.Synchronized)]
    void BuildHandlers() {
        foreach (var commandType in _builders.Keys) {
            var builder = _builders[commandType];
            var handler = builder.Build();
            _handlers.AddHandlerUntyped(commandType, handler);
        }

        _initialized = true;
    }
}
