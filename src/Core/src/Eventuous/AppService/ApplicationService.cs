// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using static Eventuous.Diagnostics.EventuousEventSource;

// ReSharper disable MemberCanBePrivate.Global

namespace Eventuous;

/// <summary>
/// Application service base class. A derived class should be scoped to handle commands for one aggregate type only.
/// </summary>
/// <typeparam name="TAggregate">The aggregate type</typeparam>
/// <typeparam name="TState">The aggregate state type</typeparam>
/// <typeparam name="TId">The aggregate identity type</typeparam>
// [PublicAPI]
public abstract class ApplicationService<TAggregate, TState, TId>
    : IApplicationService<TAggregate, TState, TId>, IApplicationService<TAggregate>
    where TAggregate : Aggregate<TState>, new()
    where TState : State<TState>, new()
    where TId : AggregateId {
    protected IAggregateStore Store { get; }

    readonly HandlersMap<TAggregate>  _handlers = new();
    readonly IdMap<TId>               _idMap    = new();
    readonly AggregateFactoryRegistry _factoryRegistry;
    readonly StreamNameMap            _streamNameMap;
    readonly TypeMapper               _typeMap;

    protected ApplicationService(
        IAggregateStore           store,
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
    protected void OnNew<TCommand>(
        GetIdFromCommand<TId, TCommand>      getId,
        ActOnAggregate<TAggregate, TCommand> action
    ) where TCommand : class {
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
    protected void OnNewAsync<TCommand>(
        GetIdFromCommand<TId, TCommand>           getId,
        ActOnAggregateAsync<TAggregate, TCommand> action
    ) where TCommand : class {
        _handlers.AddHandler(ExpectedState.New, action);
        _idMap.AddCommand(getId);
    }

    /// <summary>
    /// Register a handler for a command, which is expected to use an existing aggregate instance.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command</param>
    /// <param name="action">Action to be performed on the aggregate, given the aggregate instance and the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    protected void OnExisting<TCommand>(
        GetIdFromCommand<TId, TCommand>      getId,
        ActOnAggregate<TAggregate, TCommand> action
    ) where TCommand : class {
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
    protected void OnExistingAsync<TCommand>(
        GetIdFromCommand<TId, TCommand>           getId,
        ActOnAggregateAsync<TAggregate, TCommand> action
    ) where TCommand : class {
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
    protected void OnExistingAsync<TCommand>(
        GetIdFromCommandAsync<TId, TCommand>      getId,
        ActOnAggregateAsync<TAggregate, TCommand> action
    ) where TCommand : class {
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
    protected void OnAny<TCommand>(
        GetIdFromCommand<TId, TCommand>      getId,
        ActOnAggregate<TAggregate, TCommand> action
    ) where TCommand : class {
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
    protected void OnAny<TCommand>(
        GetIdFromCommandAsync<TId, TCommand> getId,
        ActOnAggregate<TAggregate, TCommand> action
    ) where TCommand : class {
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
    protected void OnAnyAsync<TCommand>(
        GetIdFromCommand<TId, TCommand>           getId,
        ActOnAggregateAsync<TAggregate, TCommand> action
    ) where TCommand : class {
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
    protected void OnAnyAsync<TCommand>(
        GetIdFromCommandAsync<TId, TCommand>      getId,
        ActOnAggregateAsync<TAggregate, TCommand> action
    ) where TCommand : class {
        _handlers.AddHandler(ExpectedState.Any, action);
        _idMap.AddCommand(getId);
    }

    /// <summary>
    /// Register an asynchronous handler for a command, which can figure out the aggregate instance by itself, and then return one.
    /// </summary>
    /// <param name="action">Function, which returns some aggregate instance to store</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    protected void OnAsync<TCommand>(ArbitraryActAsync<TCommand> action)
        where TCommand : class
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
    public async Task<Result<TState>> Handle(object command, CancellationToken cancellationToken) {
        var commandType = Ensure.NotNull(command).GetType();

        if (!_handlers.TryGetValue(commandType, out var registeredHandler)) {
            Log.CommandHandlerNotFound(commandType);
            var exception = new Exceptions.CommandHandlerNotFound(commandType);
            return new ErrorResult<TState>(exception);
        }

        var hasGetIdFunction = _idMap.TryGetValue(commandType, out var getId);

        if (!hasGetIdFunction || getId == null) {
            Log.CannotCalculateAggregateId(commandType);
            var exception = new Exceptions.CommandHandlerNotFound(commandType);
            return new ErrorResult<TState>(exception);
        }

        var aggregateId = await getId(command, cancellationToken).NoContext();

        var streamName = _streamNameMap.GetStreamName<TAggregate, TId>(aggregateId);

        try {
            var aggregate = registeredHandler.ExpectedState switch {
                ExpectedState.Any => await Store.LoadOrNew<TAggregate>(streamName, cancellationToken)
                    .NoContext(),
                ExpectedState.Existing => await Store.Load<TAggregate>(streamName, cancellationToken)
                    .NoContext(),
                ExpectedState.New     => Create(),
                ExpectedState.Unknown => default,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(registeredHandler.ExpectedState),
                    "Unknown expected state"
                )
            };

            var result = await registeredHandler
                .Handler(aggregate!, command, cancellationToken)
                .NoContext();

            // Zero in the global position would mean nothing, so the receiver need to check the Changes.Length
            if (result.Changes.Count == 0) return new OkResult<TState>(result.State, Array.Empty<Change>(), 0);

            var storeResult = await Store.Store(
                    streamName != default ? streamName : GetAggregateStreamName(),
                    result,
                    cancellationToken
                )
                .NoContext();

            var changes = result.Changes.Select(x => new Change(x, _typeMap.GetTypeName(x)));

            Log.CommandHandled(commandType);

            return new OkResult<TState>(result.State, changes, storeResult.GlobalPosition);
        }
        catch (Exception e) {
            Log.ErrorHandlingCommand(commandType, e);

            return new ErrorResult<TState>($"Error handling command {commandType.Name}", e);
        }

        TAggregate Create() => _factoryRegistry.CreateInstance<TAggregate, TState>();

        StreamName GetAggregateStreamName() => _streamNameMap.GetStreamName<TAggregate, TId>(aggregateId);
    }

    async Task<Result> IApplicationService.Handle(object command, CancellationToken cancellationToken) {
        var result = await Handle(command, cancellationToken).NoContext();

        return result switch {
            OkResult<TState>(var aggregateState, var enumerable, _) => new OkResult(aggregateState, enumerable),
            ErrorResult<TState> error => new ErrorResult(error.Message, error.Exception),
            _ => throw new ApplicationException("Unknown result type")
        };
    }

    public delegate Task<TAggregate> ArbitraryActAsync<in TCommand>(
        TCommand          command,
        CancellationToken cancellationToken
    );
}
