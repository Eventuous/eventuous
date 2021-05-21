using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Eventuous {
    /// <summary>
    /// Application service base class. A derived class should be scoped to handle commands for one aggregate type only.
    /// </summary>
    /// <typeparam name="T">The aggregate type</typeparam>
    /// <typeparam name="TState">The aggregate state type</typeparam>
    /// <typeparam name="TId">The aggregate identity type</typeparam>
    [PublicAPI]
    public abstract class ApplicationService<T, TState, TId>
        where T : Aggregate<TState, TId>, new()
        where TState : AggregateState<TState, TId>, new()
        where TId : AggregateId {
        protected IAggregateStore Store { get; }

        readonly HandlersMap<T> _handlers = new();
        readonly IdMap<TId>     _getId    = new();

        protected ApplicationService(IAggregateStore store) => Store = store;

        /// <summary>
        /// Register a handler for a command, which is expected to create a new aggregate instance.
        /// </summary>
        /// <param name="getId">A function to get the aggregate id from the command</param>
        /// <param name="action">Action to be performed on the aggregate, given the aggregate instance and the command</param>
        /// <typeparam name="TCommand">Command type</typeparam>
        protected void OnNew<TCommand>(Func<TCommand, TId> getId, Action<T, TCommand> action) where TCommand : class {
            _handlers.Add(
                typeof(TCommand),
                new RegisteredHandler<T>(ExpectedState.New, (aggregate, cmd, _) => AsTask(aggregate!, cmd, action))
            );

            _getId.TryAdd(typeof(TCommand), (cmd, _) => new ValueTask<TId>(getId((TCommand) cmd)));
        }

        /// <summary>
        /// Register an asynchronous handler for a command, which is expected to create a new aggregate instance.
        /// </summary>
        /// <param name="getId">A function to get the aggregate id from the command</param>
        /// <param name="action">Asynchronous action to be performed on the aggregate,
        /// given the aggregate instance and the command</param>
        /// <typeparam name="TCommand">Command type</typeparam>
        protected void OnNewAsync<TCommand>(
            Func<TCommand, TId>                        getId,
            Func<T, TCommand, CancellationToken, Task> action
        )
            where TCommand : class {
            _handlers.Add(
                typeof(TCommand),
                new RegisteredHandler<T>(ExpectedState.New, (aggregate, cmd, ct) => AsTask(aggregate!, cmd, ct, action))
            );

            _getId.TryAdd(typeof(TCommand), (cmd, _) => new ValueTask<TId>(getId((TCommand) cmd)));
        }

        /// <summary>
        /// Register a handler for a command, which is expected to use an existing aggregate instance.
        /// </summary>
        /// <param name="getId">A function to get the aggregate id from the command</param>
        /// <param name="action">Action to be performed on the aggregate, given the aggregate instance and the command</param>
        /// <typeparam name="TCommand">Command type</typeparam>
        protected void OnExisting<TCommand>(Func<TCommand, TId> getId, Action<T, TCommand> action)
            where TCommand : class {
            _handlers.Add(
                typeof(TCommand),
                new RegisteredHandler<T>(ExpectedState.Existing, (aggregate, cmd, _) => AsTask(aggregate!, cmd, action))
            );

            _getId.TryAdd(typeof(TCommand), (cmd, _) => new ValueTask<TId>(getId((TCommand) cmd)));
        }

        /// <summary>
        /// Register an asynchronous handler for a command, which is expected to use an existing aggregate instance.
        /// </summary>
        /// <param name="getId">A function to get the aggregate id from the command</param>
        /// <param name="action">Asynchronous action to be performed on the aggregate,
        /// given the aggregate instance and the command</param>
        /// <typeparam name="TCommand">Command type</typeparam>
        protected void OnExistingAsync<TCommand>(
            Func<TCommand, TId>                        getId,
            Func<T, TCommand, CancellationToken, Task> action
        )
            where TCommand : class {
            _handlers.Add(
                typeof(TCommand),
                new RegisteredHandler<T>(
                    ExpectedState.Existing,
                    (aggregate, cmd, ct) => AsTask(aggregate!, cmd, ct, action)
                )
            );

            _getId.TryAdd(typeof(TCommand), (cmd, _) => new ValueTask<TId>(getId((TCommand) cmd)));
        }

        /// <summary>
        /// Register a handler for a command, which is expected to use an a new or an existing aggregate instance.
        /// </summary>
        /// <param name="getId">A function to get the aggregate id from the command</param>
        /// <param name="action">Action to be performed on the aggregate,
        /// given the aggregate instance and the command</param>
        /// <typeparam name="TCommand">Command type</typeparam>
        protected void OnAny<TCommand>(Func<TCommand, TId> getId, Action<T, TCommand> action)
            where TCommand : class {
            _handlers.Add(
                typeof(TCommand),
                new RegisteredHandler<T>(ExpectedState.Any, (aggregate, cmd, _) => AsTask(aggregate!, cmd, action))
            );

            _getId.TryAdd(typeof(TCommand), (cmd, _) => new ValueTask<TId>(getId((TCommand) cmd)));
        }

        /// <summary>
        /// Register an asynchronous handler for a command, which is expected to use an a new or an existing aggregate instance.
        /// </summary>
        /// <param name="getId">A function to get the aggregate id from the command</param>
        /// <param name="action">Asynchronous action to be performed on the aggregate,
        /// given the aggregate instance and the command</param>
        /// <typeparam name="TCommand">Command type</typeparam>
        protected void OnAnyAsync<TCommand>(
            Func<TCommand, TId>                        getId,
            Func<T, TCommand, CancellationToken, Task> action
        )
            where TCommand : class {
            _handlers.Add(
                typeof(TCommand),
                new RegisteredHandler<T>(ExpectedState.Any, (aggregate, cmd, ct) => AsTask(aggregate!, cmd, ct, action))
            );

            _getId.TryAdd(typeof(TCommand), (cmd, _) => new ValueTask<TId>(getId((TCommand) cmd)));
        }

        /// <summary>
        /// Register a handler for a command, which is expected to use an a new or an existing aggregate instance.
        /// </summary>
        /// <param name="getId">Asynchronous function to get the aggregate id from the command</param>
        /// <param name="action">Action to be performed on the aggregate,
        /// given the aggregate instance and the command</param>
        /// <typeparam name="TCommand">Command type</typeparam>
        protected void OnAny<TCommand>(Func<TCommand, CancellationToken, Task<TId>> getId, Action<T, TCommand> action)
            where TCommand : class {
            _handlers.Add(
                typeof(TCommand),
                new RegisteredHandler<T>(ExpectedState.Any, (aggregate, cmd, _) => AsTask(aggregate!, cmd, action))
            );

            _getId.TryAdd(typeof(TCommand), async (cmd, ct) => await getId((TCommand) cmd, ct).Ignore());
        }

        /// <summary>
        /// Register an asynchronous handler for a command, which is expected to use an a new or an existing aggregate instance.
        /// </summary>
        /// <param name="getId">Asynchronous function to get the aggregate id from the command</param>
        /// <param name="action">Asynchronous action to be performed on the aggregate,
        /// given the aggregate instance and the command</param>
        /// <typeparam name="TCommand">Command type</typeparam>
        protected void OnAnyAsync<TCommand>(
            Func<TCommand, CancellationToken, Task<TId>> getId,
            Func<T, TCommand, CancellationToken, Task>   action
        )
            where TCommand : class {
            _handlers.Add(
                typeof(TCommand),
                new RegisteredHandler<T>(ExpectedState.Any, (aggregate, cmd, ct) => AsTask(aggregate!, cmd, ct, action))
            );

            _getId.TryAdd(typeof(TCommand), async (cmd, ct) => await getId((TCommand) cmd, ct).Ignore());
        }

        /// <summary>
        /// Register an asynchronous handler for a command, which can figure out the aggregate instance by itself, and then return one.
        /// </summary>
        /// <param name="action">Function, which returns some aggregate instance to store</param>
        /// <typeparam name="TCommand">Command type</typeparam>
        protected void OnAsync<TCommand>(Func<TCommand, CancellationToken, Task<T>> action) where TCommand : class
            => _handlers.Add(
                typeof(TCommand),
                new RegisteredHandler<T>(
                    ExpectedState.Unknown,
                    async (_, cmd, ct) => await action((TCommand) cmd, ct)
                )
            );

        static ValueTask<T> AsTask<TCommand>(T aggregate, object cmd, Action<T, TCommand> action) {
            action(aggregate, (TCommand) cmd);
            return new ValueTask<T>(aggregate);
        }

        static async ValueTask<T> AsTask<TCommand>(
            T                                          aggregate,
            object                                     cmd,
            CancellationToken                          cancellationToken,
            Func<T, TCommand, CancellationToken, Task> action
        ) {
            await action(aggregate, (TCommand) cmd, cancellationToken).Ignore();
            return aggregate;
        }

        /// <summary>
        /// The generic command handler. Call this function from your edge (API).
        /// </summary>
        /// <param name="command">Command to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <typeparam name="TCommand">Command type</typeparam>
        /// <returns><see cref="Result{T,TState,TId}"/> of the execution</returns>
        /// <exception cref="Exceptions.CommandHandlerNotFound"></exception>
        public async Task<Result<T, TState, TId>> Handle<TCommand>(
            TCommand          command,
            CancellationToken cancellationToken
        )
            where TCommand : class {
            if (!_handlers.TryGetValue(typeof(TCommand), out var registeredHandler)) {
                throw new Exceptions.CommandHandlerNotFound(typeof(TCommand));
            }

            var id = await _getId[typeof(TCommand)](command, cancellationToken).Ignore();

            var aggregate = registeredHandler.ExpectedState switch {
                ExpectedState.Any      => await TryLoad().Ignore(),
                ExpectedState.Existing => await Load().Ignore(),
                ExpectedState.New      => Create(),
                ExpectedState.Unknown  => default,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(registeredHandler.ExpectedState),
                    "Unknown expected state"
                )
            };

            var result = await registeredHandler.Handler(aggregate, command, cancellationToken).Ignore();

            var storeResult = await Store.Store(result, cancellationToken).Ignore();

            return new OkResult<T, TState, TId>(result.State, result.Changes, storeResult.GlobalPosition);

            Task<T> Load() => Store.Load<T, TState, TId>(id, cancellationToken);

            async Task<T> TryLoad() {
                try {
                    return await Load().Ignore();
                }
                catch (Exceptions.AggregateNotFound<T>) {
                    return Create();
                }
            }

            T Create() {
                var newInstance = new T();
                newInstance.State = newInstance.State.SetId(id);
                return newInstance;
            }
        }
    }

    record RegisteredHandler<T>(ExpectedState ExpectedState, Func<T?, object, CancellationToken, ValueTask<T>> Handler);

    class HandlersMap<T> : Dictionary<Type, RegisteredHandler<T>> { }

    class IdMap<T> : Dictionary<Type, Func<object, CancellationToken, ValueTask<T>>> { }

    enum ExpectedState {
        New,
        Existing,
        Any,
        Unknown
    }
}