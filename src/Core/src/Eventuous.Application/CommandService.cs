// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

using static Diagnostics.ApplicationEventSource;

/// <summary>
/// Command service base class. A derived class should be scoped to handle commands for one aggregate type only.
/// </summary>
/// <typeparam name="TAggregate">The aggregate type</typeparam>
/// <typeparam name="TState">The aggregate state type</typeparam>
/// <typeparam name="TId">The aggregate identity type</typeparam>
// [PublicAPI]
public abstract partial class CommandService<TAggregate, TState, TId> : ICommandService<TAggregate, TState, TId>, ICommandService<TAggregate>
    where TAggregate : Aggregate<TState>, new()
    where TState : State<TState>, new()
    where TId : Id {
    [PublicAPI]
    protected IAggregateStore? Store { get; }

    readonly HandlersMap<TAggregate, TId> _handlers = new();
    readonly IdMap<TId>               _idMap           = new();
    readonly AggregateFactoryRegistry     _factoryRegistry;
    readonly StreamNameMap                _streamNameMap;
    readonly TypeMapper                   _typeMap;

    protected CommandService(
        IAggregateStore?          store,
        AggregateFactoryRegistry? factoryRegistry = null,
        StreamNameMap?            streamNameMap   = null,
        TypeMapper?               typeMap         = null
    ) {
        _factoryRegistry = factoryRegistry ?? AggregateFactoryRegistry.Instance;
        _streamNameMap   = streamNameMap   ?? new StreamNameMap();
        _typeMap         = typeMap         ?? TypeMap.Instance;
        Store            = store;
    }

    /// <summary>
    /// Register a handler for a command, which is expected to create a new aggregate instance.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command</param>
    /// <param name="action">Action to be performed on the aggregate, given the aggregate instance and the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    protected void OnNew<TCommand>(GetIdFromCommand<TId, TCommand> getId, ActOnAggregate<TAggregate, TCommand> action) where TCommand : class {
        _handlers.AddHandler(ExpectedState.New, action);
        _idMap.AddCommand(getId);
    }

    /// <summary>
    /// Register an asynchronous handler for a command, which is expected to create a new aggregate instance.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command</param>
    /// <param name="action">Asynchronous action to be performed on the aggregate,
    /// given the aggregate instance and the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    protected void OnNewAsync<TCommand>(GetIdFromCommand<TId, TCommand> getId, ActOnAggregateAsync<TAggregate, TCommand> action) where TCommand : class {
        _handlers.AddHandler(ExpectedState.New, action);
        _idMap.AddCommand(getId);
    }

    /// <summary>
    /// Register a handler for a command, which is expected to use an existing aggregate instance.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command</param>
    /// <param name="action">Action to be performed on the aggregate, given the aggregate instance and the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    protected void OnExisting<TCommand>(GetIdFromCommand<TId, TCommand> getId, ActOnAggregate<TAggregate, TCommand> action) where TCommand : class {
        _handlers.AddHandler(ExpectedState.Existing, action);
        _idMap.AddCommand(getId);
    }

    /// <summary>
    /// Register an asynchronous handler for a command, which is expected to use an existing aggregate instance.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command</param>
    /// <param name="action">Asynchronous action to be performed on the aggregate,
    /// given the aggregate instance and the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    [PublicAPI]
    protected void OnExistingAsync<TCommand>(GetIdFromCommand<TId, TCommand> getId, ActOnAggregateAsync<TAggregate, TCommand> action) where TCommand : class {
        _handlers.AddHandler(ExpectedState.Existing, action);
        _idMap.AddCommand(getId);
    }

    /// <summary>
    /// Register an asynchronous handler for a command, which is expected to use an existing aggregate instance.
    /// </summary>
    /// <param name="getId">Asynchronous function to get the aggregate id from the command</param>
    /// <param name="action">Asynchronous action to be performed on the aggregate,
    /// given the aggregate instance and the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    [PublicAPI]
    protected void OnExistingAsync<TCommand>(GetIdFromCommandAsync<TId, TCommand> getId, ActOnAggregateAsync<TAggregate, TCommand> action)
        where TCommand : class {
        _handlers.AddHandler(ExpectedState.Existing, action);
        _idMap.AddCommand(getId);
    }

    /// <summary>
    /// Register a handler for a command, which is expected to use an a new or an existing aggregate instance.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command</param>
    /// <param name="action">Action to be performed on the aggregate,
    /// given the aggregate instance and the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    protected void OnAny<TCommand>(GetIdFromCommand<TId, TCommand> getId, ActOnAggregate<TAggregate, TCommand> action) where TCommand : class {
        _handlers.AddHandler(ExpectedState.Any, action);
        _idMap.AddCommand(getId);
    }

    /// <summary>
    /// Register a handler for a command, which is expected to use an a new or an existing aggregate instance.
    /// </summary>
    /// <param name="getId">Asynchronous function to get the aggregate id from the command</param>
    /// <param name="action">Action to be performed on the aggregate,
    /// given the aggregate instance and the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    [PublicAPI]
    protected void OnAny<TCommand>(GetIdFromCommandAsync<TId, TCommand> getId, ActOnAggregate<TAggregate, TCommand> action) where TCommand : class {
        _handlers.AddHandler(ExpectedState.Any, action);
        _idMap.AddCommand(getId);
    }

    /// <summary>
    /// Register an asynchronous handler for a command, which is expected to use an a new or an existing aggregate instance.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command</param>
    /// <param name="action">Asynchronous action to be performed on the aggregate,
    /// given the aggregate instance and the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    [PublicAPI]
    protected void OnAnyAsync<TCommand>(GetIdFromCommand<TId, TCommand> getId, ActOnAggregateAsync<TAggregate, TCommand> action) where TCommand : class {
        _handlers.AddHandler(ExpectedState.Any, action);
        _idMap.AddCommand(getId);
    }

    /// <summary>
    /// Register an asynchronous handler for a command, which is expected to use an a new or an existing aggregate instance.
    /// </summary>
    /// <param name="getId">Asynchronous function to get the aggregate id from the command</param>
    /// <param name="action">Asynchronous action to be performed on the aggregate,
    /// given the aggregate instance and the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    [PublicAPI]
    protected void OnAnyAsync<TCommand>(GetIdFromCommandAsync<TId, TCommand> getId, ActOnAggregateAsync<TAggregate, TCommand> action) where TCommand : class {
        _handlers.AddHandler(ExpectedState.Any, action);
        _idMap.AddCommand(getId);
    }

    /// <summary>
    /// Register an asynchronous handler for a command, which can figure out the aggregate instance by itself, and then return one.
    /// </summary>
    /// <param name="action">Function, which returns some aggregate instance to store</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    [PublicAPI]
    protected void OnAsync<TCommand>(ArbitraryActAsync<TCommand> action) where TCommand : class
        => _handlers.AddHandler<TCommand>(
            new RegisteredHandler<TAggregate>(
                ExpectedState.Unknown,
                async (_, cmd, ct) => await action((TCommand)cmd, ct).NoContext()
            )
        );

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
            var exception = new Exceptions.CommandHandlerNotFound<TCommand>();

            return new ErrorResult<TState>(exception);
        }

        var aggregateId = await registeredHandler.GetId(command, cancellationToken).NoContext();
        var store       = registeredHandler.ResolveStore(command);
        var hasGetIdFunction = _idMap.TryGet<TCommand>(out var getId);

        if (!hasGetIdFunction || getId == null) {
            Log.CannotCalculateAggregateId<TCommand>();
            var exception = new Exceptions.CommandHandlerNotFound<TCommand>();

            return new ErrorResult<TState>(exception);
        }

        var aggregateId = await getId(command, cancellationToken).NoContext();

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

        TAggregate Create(TId id) => _factoryRegistry.CreateInstance<TAggregate, TState>().WithId<TAggregate, TState, TId>(id);

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

    public delegate Task<TAggregate> ArbitraryActAsync<in TCommand>(
            TCommand          command,
            CancellationToken cancellationToken
        );
}

public delegate IAggregateStore ResolveStore<in TCommand>(TCommand command) where TCommand : class;
