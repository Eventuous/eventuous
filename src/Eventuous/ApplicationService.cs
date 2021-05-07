using System;
using System.Collections.Generic;
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
        readonly IAggregateStore _store;
        readonly HandlersMap<T>  _handlers = new();
        readonly IdMap<TId>      _getId    = new();

        protected ApplicationService(IAggregateStore store) => _store = store;

        /// <summary>
        /// Register a handler for a command, which is expected to create a new aggregate instance.
        /// </summary>
        /// <param name="action">Action to be performed on the aggregate, given the aggregate instance and the command</param>
        /// <typeparam name="TCommand">Command type</typeparam>
        protected void OnNew<TCommand>(Action<T, TCommand> action) where TCommand : class
            => _handlers.Add(
                typeof(TCommand),
                new RegisteredHandler<T>(ExpectedState.New, (aggregate, cmd) => AsTask(aggregate, cmd, action))
            );

        /// <summary>
        /// Register an asynchronous handler for a command, which is expected to create a new aggregate instance.
        /// </summary>
        /// <param name="action">Asynchronous action to be performed on the aggregate,
        /// given the aggregate instance and the command</param>
        /// <typeparam name="TCommand">Command type</typeparam>
        protected void OnNewAsync<TCommand>(Func<T, TCommand, Task> action) where TCommand : class
            => _handlers.Add(
                typeof(TCommand),
                new RegisteredHandler<T>(ExpectedState.New, (aggregate, cmd) => AsTask(aggregate, cmd, action))
            );

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
                new RegisteredHandler<T>(ExpectedState.Existing, (aggregate, cmd) => AsTask(aggregate, cmd, action))
            );

            _getId.TryAdd(typeof(TCommand), cmd => new ValueTask<TId>(getId((TCommand) cmd)));
        }

        /// <summary>
        /// Register an asynchronous handler for a command, which is expected to use an existing aggregate instance.
        /// </summary>
        /// <param name="getId">A function to get the aggregate id from the command</param>
        /// <param name="action">Asynchronous action to be performed on the aggregate,
        /// given the aggregate instance and the command</param>
        /// <typeparam name="TCommand">Command type</typeparam>
        protected void OnExistingAsync<TCommand>(Func<TCommand, TId> getId, Func<T, TCommand, Task> action)
            where TCommand : class {
            _handlers.Add(
                typeof(TCommand),
                new RegisteredHandler<T>(ExpectedState.Existing, (aggregate, cmd) => AsTask(aggregate, cmd, action))
            );

            _getId.TryAdd(typeof(TCommand), cmd => new ValueTask<TId>(getId((TCommand) cmd)));
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
                new RegisteredHandler<T>(ExpectedState.Any, (aggregate, cmd) => AsTask(aggregate, cmd, action))
            );

            _getId.TryAdd(typeof(TCommand), cmd => new ValueTask<TId>(getId((TCommand) cmd)));
        }

        /// <summary>
        /// Register an asynchronous handler for a command, which is expected to use an a new or an existing aggregate instance.
        /// </summary>
        /// <param name="getId">A function to get the aggregate id from the command</param>
        /// <param name="action">Asynchronous action to be performed on the aggregate,
        /// given the aggregate instance and the command</param>
        /// <typeparam name="TCommand">Command type</typeparam>
        protected void OnAnyAsync<TCommand>(Func<TCommand, TId> getId, Func<T, TCommand, Task> action)
            where TCommand : class {
            _handlers.Add(
                typeof(TCommand),
                new RegisteredHandler<T>(ExpectedState.Any, (aggregate, cmd) => AsTask(aggregate, cmd, action))
            );

            _getId.TryAdd(typeof(TCommand), cmd => new ValueTask<TId>(getId((TCommand) cmd)));
        }

        /// <summary>
        /// Register a handler for a command, which is expected to use an a new or an existing aggregate instance.
        /// </summary>
        /// <param name="getId">Asynchronous function to get the aggregate id from the command</param>
        /// <param name="action">Action to be performed on the aggregate,
        /// given the aggregate instance and the command</param>
        /// <typeparam name="TCommand">Command type</typeparam>
        protected void OnAny<TCommand>(Func<TCommand, Task<TId>> getId, Action<T, TCommand> action)
            where TCommand : class {
            _handlers.Add(
                typeof(TCommand),
                new RegisteredHandler<T>(ExpectedState.Any, (aggregate, cmd) => AsTask(aggregate, cmd, action))
            );

            _getId.TryAdd(typeof(TCommand), async cmd => await getId((TCommand) cmd));
        }

        /// <summary>
        /// Register an asynchronous handler for a command, which is expected to use an a new or an existing aggregate instance.
        /// </summary>
        /// <param name="getId">Asynchronous function to get the aggregate id from the command</param>
        /// <param name="action">Asynchronous action to be performed on the aggregate,
        /// given the aggregate instance and the command</param>
        /// <typeparam name="TCommand">Command type</typeparam>
        protected void OnAnyAsync<TCommand>(Func<TCommand, Task<TId>> getId, Func<T, TCommand, Task> action)
            where TCommand : class {
            _handlers.Add(
                typeof(TCommand),
                new RegisteredHandler<T>(ExpectedState.Any, (aggregate, cmd) => AsTask(aggregate, cmd, action))
            );

            _getId.TryAdd(typeof(TCommand), async cmd => await getId((TCommand) cmd));
        }

        static ValueTask AsTask<TCommand>(T aggregate, object cmd, Action<T, TCommand> action) {
            action(aggregate, (TCommand) cmd);
            return ValueTask.CompletedTask;
        }

        static async ValueTask AsTask<TCommand>(T aggregate, object cmd, Func<T, TCommand, Task> action) {
            await action(aggregate, (TCommand) cmd);
        }

        /// <summary>
        /// The generic command handler. Call this function from your edge (API).
        /// </summary>
        /// <param name="command">Command to execute</param>
        /// <typeparam name="TCommand">Command type</typeparam>
        /// <returns><see cref="Result{T,TState,TId}"/> of the execution</returns>
        /// <exception cref="Exceptions.CommandHandlerNotFound"></exception>
        public async Task<Result<T, TState, TId>> Handle<TCommand>(TCommand command)
            where TCommand : class {
            if (!_handlers.TryGetValue(typeof(TCommand), out var registeredHandler)) {
                throw new Exceptions.CommandHandlerNotFound(typeof(TCommand));
            }

            var id = await _getId[typeof(TCommand)](command);

            var aggregate = registeredHandler.ExpectedState switch {
                ExpectedState.Any      => await TryLoad(),
                ExpectedState.Existing => await Load(),
                ExpectedState.New      => Create()
            };

            await registeredHandler.Handler(aggregate, command);

            await _store.Store(aggregate);

            return new OkResult<T, TState, TId>(aggregate.State, aggregate.Changes);

            Task<T> Load() => _store.Load<T>(id);

            async Task<T> TryLoad() {
                try {
                    return await Load();
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

    record RegisteredHandler<T>(ExpectedState ExpectedState, Func<T, object, ValueTask> Handler);

    class HandlersMap<T> : Dictionary<Type, RegisteredHandler<T>> { }

    class IdMap<T> : Dictionary<Type, Func<object, ValueTask<T>>> { }

    enum ExpectedState {
        New,
        Existing,
        Any
    }
}