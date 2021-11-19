using Eventuous.Diagnostics;

namespace Eventuous;

/// <summary>
/// Application service base class. A derived class should be scoped to handle commands for one aggregate type only.
/// </summary>
/// <typeparam name="T">The aggregate type</typeparam>
/// <typeparam name="TState">The aggregate state type</typeparam>
/// <typeparam name="TId">The aggregate identity type</typeparam>
[PublicAPI]
public abstract class ApplicationService<T, TState, TId> : IApplicationService<TState, TId>,
    IApplicationService<T>
    where T : Aggregate<TState, TId>, new()
    where TState : AggregateState<TState, TId>, new()
    where TId : AggregateId {
    protected IAggregateStore Store { get; }

    readonly HandlersMap<T>           _handlers = new();
    readonly IdMap<TId>               _getId    = new();
    readonly AggregateFactoryRegistry _factoryRegistry;

    protected ApplicationService(
        IAggregateStore           store,
        AggregateFactoryRegistry? factoryRegistry = null
    ) {
        _factoryRegistry = factoryRegistry ?? AggregateFactoryRegistry.Instance;
        Store            = store;
    }

    /// <summary>
    /// Register a handler for a command, which is expected to create a new aggregate instance.
    /// </summary>
    /// <param name="action">Action to be performed on the aggregate, given the aggregate instance and the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    protected void OnNew<TCommand>(ActOnAggregate<TCommand> action)
        where TCommand : class
        => _handlers.AddHandler<TCommand>(
            new RegisteredHandler<T>(
                ExpectedState.New,
                (aggregate, cmd, _) => SyncAsTask(aggregate, cmd, action)
            )
        );

    /// <summary>
    /// Register an asynchronous handler for a command, which is expected to create a new aggregate instance.
    /// </summary>
    /// <param name="action">Asynchronous action to be performed on the aggregate,
    /// given the aggregate instance and the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    protected void OnNewAsync<TCommand>(ActOnAggregateAsync<TCommand> action)
        where TCommand : class
        => _handlers.AddHandler<TCommand>(
            new RegisteredHandler<T>(
                ExpectedState.New,
                (aggregate, cmd, ct) => AsTask(aggregate, cmd, action, ct)
            )
        );

    /// <summary>
    /// Register a handler for a command, which is expected to use an existing aggregate instance.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command</param>
    /// <param name="action">Action to be performed on the aggregate, given the aggregate instance and the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    protected void OnExisting<TCommand>(
        GetIdFromCommand<TCommand> getId,
        ActOnAggregate<TCommand>   action
    )
        where TCommand : class {
        _handlers.AddHandler<TCommand>(
            new RegisteredHandler<T>(
                ExpectedState.Existing,
                (aggregate, cmd, _) => SyncAsTask(aggregate, cmd, action)
            )
        );

        _getId.TryAdd(typeof(TCommand), (cmd, _) => new ValueTask<TId>(getId((TCommand)cmd)));
    }

    /// <summary>
    /// Register an asynchronous handler for a command, which is expected to use an existing aggregate instance.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command</param>
    /// <param name="action">Asynchronous action to be performed on the aggregate,
    /// given the aggregate instance and the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    protected void OnExistingAsync<TCommand>(
        GetIdFromCommand<TCommand>    getId,
        ActOnAggregateAsync<TCommand> action
    )
        where TCommand : class {
        _handlers.AddHandler<TCommand>(
            new RegisteredHandler<T>(
                ExpectedState.Existing,
                (aggregate, cmd, ct) => AsTask(aggregate, cmd, action, ct)
            )
        );

        _getId.TryAdd(typeof(TCommand), (cmd, _) => new ValueTask<TId>(getId((TCommand)cmd)));
    }

    /// <summary>
    /// Register a handler for a command, which is expected to use an a new or an existing aggregate instance.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command</param>
    /// <param name="action">Action to be performed on the aggregate,
    /// given the aggregate instance and the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    protected void OnAny<TCommand>(
        GetIdFromCommand<TCommand> getId,
        ActOnAggregate<TCommand>   action
    )
        where TCommand : class {
        _handlers.AddHandler<TCommand>(
            new RegisteredHandler<T>(
                ExpectedState.Any,
                (aggregate, cmd, _) => SyncAsTask(aggregate, cmd, action)
            )
        );

        _getId.TryAdd(typeof(TCommand), (cmd, _) => new ValueTask<TId>(getId((TCommand)cmd)));
    }

    /// <summary>
    /// Register an asynchronous handler for a command, which is expected to use an a new or an existing aggregate instance.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command</param>
    /// <param name="action">Asynchronous action to be performed on the aggregate,
    /// given the aggregate instance and the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    protected void OnAnyAsync<TCommand>(
        GetIdFromCommand<TCommand>    getId,
        ActOnAggregateAsync<TCommand> action
    )
        where TCommand : class {
        _handlers.AddHandler<TCommand>(
            new RegisteredHandler<T>(
                ExpectedState.Any,
                (aggregate, cmd, ct) => AsTask(aggregate, cmd, action, ct)
            )
        );

        _getId.TryAdd(typeof(TCommand), (cmd, _) => new ValueTask<TId>(getId((TCommand)cmd)));
    }

    /// <summary>
    /// Register a handler for a command, which is expected to use an a new or an existing aggregate instance.
    /// </summary>
    /// <param name="getId">Asynchronous function to get the aggregate id from the command</param>
    /// <param name="action">Action to be performed on the aggregate,
    /// given the aggregate instance and the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    protected void OnAny<TCommand>(
        GetIdFromCommandAsync<TCommand> getId,
        ActOnAggregate<TCommand>        action
    )
        where TCommand : class {
        _handlers.AddHandler<TCommand>(
            new RegisteredHandler<T>(
                ExpectedState.Any,
                (aggregate, cmd, _) => SyncAsTask(aggregate, cmd, action)
            )
        );

        _getId.TryAdd(
            typeof(TCommand),
            async (cmd, ct) => await getId((TCommand)cmd, ct).NoContext()
        );
    }

    /// <summary>
    /// Register an asynchronous handler for a command, which is expected to use an a new or an existing aggregate instance.
    /// </summary>
    /// <param name="getId">Asynchronous function to get the aggregate id from the command</param>
    /// <param name="action">Asynchronous action to be performed on the aggregate,
    /// given the aggregate instance and the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    protected void OnAnyAsync<TCommand>(
        GetIdFromCommandAsync<TCommand> getId,
        ActOnAggregateAsync<TCommand>   action
    )
        where TCommand : class {
        _handlers.AddHandler<TCommand>(
            new RegisteredHandler<T>(
                ExpectedState.Any,
                (aggregate, cmd, ct) => AsTask(aggregate, cmd, action, ct)
            )
        );

        _getId.TryAdd(
            typeof(TCommand),
            async (cmd, ct) => await getId((TCommand)cmd, ct).NoContext()
        );
    }

    /// <summary>
    /// Register an asynchronous handler for a command, which can figure out the aggregate instance by itself, and then return one.
    /// </summary>
    /// <param name="action">Function, which returns some aggregate instance to store</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    protected void OnAsync<TCommand>(ArbitraryActAsync<TCommand> action)
        where TCommand : class
        => _handlers.AddHandler<TCommand>(
            new RegisteredHandler<T>(
                ExpectedState.Unknown,
                async (_, cmd, ct) => await action((TCommand)cmd, ct).NoContext()
            )
        );

    static ValueTask<T> SyncAsTask<TCommand>(
        T                        aggregate,
        object                   cmd,
        ActOnAggregate<TCommand> action
    ) {
        action(aggregate, (TCommand)cmd);
        return new ValueTask<T>(aggregate);
    }

    static async ValueTask<T> AsTask<TCommand>(
        T                             aggregate,
        object                        cmd,
        ActOnAggregateAsync<TCommand> action,
        CancellationToken             cancellationToken
    ) {
        await action(aggregate, (TCommand)cmd, cancellationToken).NoContext();
        return aggregate;
    }

    /// <summary>
    /// The generic command handler. Call this function from your edge (API).
    /// </summary>
    /// <param name="command">Command to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <returns><see cref="Result{TState,TId}"/> of the execution</returns>
    /// <exception cref="Exceptions.CommandHandlerNotFound{TCommand}"></exception>
    public async Task<Result<TState, TId>> Handle<TCommand>(
        TCommand          command,
        CancellationToken cancellationToken
    )
        where TCommand : class {
        if (!_handlers.TryGetValue(typeof(TCommand), out var registeredHandler)) {
            EventuousEventSource.Log.CommandHandlerNotFound<TCommand>();
            var exception = new Exceptions.CommandHandlerNotFound<TCommand>();
            return new ErrorResult<TState, TId>(exception);
        }

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

            var storeResult = await Store.Store(result, cancellationToken).NoContext();

            var changes = result.Changes.Select(x => new Change(x, TypeMap.GetTypeName(x)));
            return new OkResult<TState, TId>(result.State, changes, storeResult.GlobalPosition);
        }
        catch (Exception e) {
            EventuousEventSource.Log.ErrorHandlingCommand<TCommand>(e);

            return new ErrorResult<TState, TId>(
                $"Error handling command {typeof(TCommand).Name}",
                e
            );
        }

        async Task<T> Load() {
            var id = await _getId[typeof(TCommand)](command, cancellationToken).NoContext();
            return await Store.Load<T, TState, TId>(id, cancellationToken).NoContext();
        }

        async Task<T> TryLoad() {
            var id     = await _getId[typeof(TCommand)](command, cancellationToken).NoContext();
            var exists = await Store.Exists<T>(id, cancellationToken).NoContext();
            return exists ? await Load().NoContext() : Create();
        }

        T Create() => _factoryRegistry.CreateInstance<T, TState, TId>();
    }

    public delegate Task ActOnAggregateAsync<in TCommand>(
        T                 aggregate,
        TCommand          command,
        CancellationToken cancellationToken
    );

    public delegate void ActOnAggregate<in TCommand>(T aggregate, TCommand command);

    public delegate Task<T> ArbitraryActAsync<in TCommand>(
        TCommand          command,
        CancellationToken cancellationToken
    );

    public delegate TId GetIdFromCommand<in TCommand>(TCommand command);

    public delegate Task<TId> GetIdFromCommandAsync<in TCommand>(
        TCommand          command,
        CancellationToken cancellationToken
    );

    async Task<Result> IApplicationService<T>.Handle<TCommand>(
        TCommand          command,
        CancellationToken cancellationToken
    ) {
        var (state, enumerable) = await Handle(command, cancellationToken).NoContext();
        return new Result(state, enumerable);
    }
}

record RegisteredHandler<T>(
    ExpectedState                                    ExpectedState,
    Func<T, object, CancellationToken, ValueTask<T>> Handler
);

class HandlersMap<T> : Dictionary<Type, RegisteredHandler<T>> {
    public void AddHandler<TCommand>(RegisteredHandler<T> handler) {
        if (ContainsKey(typeof(TCommand))) {
            EventuousEventSource.Log.CommandHandlerAlreadyRegistered<TCommand>();
            throw new Exceptions.CommandHandlerAlreadyRegistered<TCommand>();
        }

        Add(typeof(TCommand), handler);
    }
}

class IdMap<T> : Dictionary<Type, Func<object, CancellationToken, ValueTask<T>>> { }

enum ExpectedState {
    New,
    Existing,
    Any,
    Unknown
}