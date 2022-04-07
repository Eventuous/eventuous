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
    where TAggregate : Aggregate<TState, TId>, new()
    where TState : AggregateState<TState, TId>, new()
    where TId : AggregateId {
    protected IAggregateStore Store { get; }

    readonly HandlersMap<TAggregate>  _handlers = new();
    readonly IdMap<TId>               _idMap    = new();
    readonly AggregateFactoryRegistry _factoryRegistry;
    readonly StreamNameMap           _streamNameMap;

    protected ApplicationService(IAggregateStore store, AggregateFactoryRegistry? factoryRegistry = null, StreamNameMap? streamNameMap = null) {
        _factoryRegistry = factoryRegistry ?? AggregateFactoryRegistry.Instance;
        _streamNameMap   = streamNameMap   ?? new StreamNameMap();
        Store            = store;
    }

    /// <summary>
    /// Register a handler for a command, which is expected to create a new aggregate instance.
    /// </summary>
    /// <param name="action">Action to be performed on the aggregate, given the aggregate instance and the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    protected void OnNew<TCommand>(ActOnAggregate<TAggregate, TCommand> action) where TCommand : class
        => _handlers.AddHandler(ExpectedState.New, action);

    /// <summary>
    /// Register an asynchronous handler for a command, which is expected to create a new aggregate instance.
    /// </summary>
    /// <param name="action">Asynchronous action to be performed on the aggregate,
    /// given the aggregate instance and the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    protected void OnNewAsync<TCommand>(ActOnAggregateAsync<TAggregate, TCommand> action) where TCommand : class
        => _handlers.AddHandler(ExpectedState.New, action);

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
    /// <returns><see cref="Result{TState,TId}"/> of the execution</returns>
    /// <exception cref="Exceptions.CommandHandlerNotFound{TCommand}"></exception>
    public async Task<Result<TState, TId>> Handle(object command, CancellationToken cancellationToken) {
        var commandType = Ensure.NotNull(command).GetType();

        if (!_handlers.TryGetValue(commandType, out var registeredHandler)) {
            Log.CommandHandlerNotFound(commandType);
            var exception = new Exceptions.CommandHandlerNotFound(commandType);
            return new ErrorResult<TState, TId>(exception);
        }

        var hasGetIdFunction        = _idMap.TryGetValue(commandType, out var getId);
        var shouldResolveStreamName = registeredHandler.ExpectedState is ExpectedState.Existing or ExpectedState.Any;

        if ((!hasGetIdFunction || getId == null) && shouldResolveStreamName) {
            Log.CannotCalculateAggregateId(commandType);
            var exception = new Exceptions.CommandHandlerNotFound(commandType);
            return new ErrorResult<TState, TId>(exception);
        }

        var aggregateId = hasGetIdFunction ? await getId!(command, cancellationToken).NoContext() : default;

        var streamName = shouldResolveStreamName && aggregateId != default
            ? _streamNameMap.GetStreamName<TAggregate, TState, TId>(aggregateId)
            : default;

        try {
            var aggregate = registeredHandler.ExpectedState switch {
                ExpectedState.Any      => await TryLoad().NoContext(),
                ExpectedState.Existing => await Load().NoContext(),
                ExpectedState.New      => Create(),
                ExpectedState.Unknown  => default,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(registeredHandler.ExpectedState),
                    "Unknown expected state"
                )
            };

            var result = await registeredHandler
                .Handler(aggregate!, command, cancellationToken)
                .NoContext();

            var storeResult = await Store.Store(
                    streamName != default ? streamName : GetAggregateStreamName(result),
                    result,
                    cancellationToken
                )
                .NoContext();

            var changes = result.Changes.Select(x => new Change(x, TypeMap.GetTypeName(x)));

            Log.CommandHandled(commandType);

            return new OkResult<TState, TId>(result.State, changes, storeResult.GlobalPosition);
        }
        catch (Exception e) {
            Log.ErrorHandlingCommand(commandType, e);

            return new ErrorResult<TState, TId>($"Error handling command {commandType.Name}", e);
        }

        Task<TAggregate> Load() => Store.Load<TAggregate, TState, TId>(streamName, cancellationToken);

        async Task<TAggregate> TryLoad() {
            var exists = await Store.Exists<TAggregate>(streamName, cancellationToken).NoContext();
            return exists ? await Load().NoContext() : Create();
        }

        TAggregate Create() => _factoryRegistry.CreateInstance<TAggregate, TState, TId>();

        StreamName GetAggregateStreamName(TAggregate aggregate) {
            var id = aggregate.State.Id;
            return _streamNameMap.GetStreamName<TAggregate, TState, TId>(id);
        }
    }

    async Task<Result> IApplicationService.Handle(object command, CancellationToken cancellationToken) {
        var result = await Handle(command, cancellationToken).NoContext();

        return result switch {
            OkResult<TState, TId>(var aggregateState, var enumerable, _) => new OkResult(aggregateState, enumerable),
            ErrorResult<TState, TId> error => new ErrorResult(error.Message, error.Exception),
            _ => throw new ApplicationException("Unknown result type")
        };
    }

    public delegate Task<TAggregate> ArbitraryActAsync<in TCommand>(
        TCommand          command,
        CancellationToken cancellationToken
    );
}
