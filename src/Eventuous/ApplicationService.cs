using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Eventuous {
    [PublicAPI]
    public abstract class ApplicationService<T, TState, TId>
        where T : Aggregate<TState, TId>, new()
        where TState : AggregateState<TState, TId>, new()
        where TId : AggregateId {
        readonly IAggregateStore _store;
        readonly HandlersMap<T>  _handlers = new();
        readonly IdMap<TId>      _getId    = new();

        protected ApplicationService(IAggregateStore store) => _store = store;

        protected void OnNew<TCommand>(Action<T, TCommand> action) where TCommand : class
            => _handlers.Add(
                new MapKey(typeof(TCommand), ExpectedState.New),
                (aggregate, cmd) => action(aggregate, (TCommand) cmd)
            );

        protected void OnExisting<TCommand>(Func<TCommand, TId> getId, Action<T, TCommand> action)
            where TCommand : class {
            _handlers.Add(
                new MapKey(typeof(TCommand), ExpectedState.Existing),
                (aggregate, cmd) => action(aggregate, (TCommand) cmd)
            );

            _getId.TryAdd(typeof(TCommand), cmd => getId((TCommand) cmd));
        }

        protected void OnAny<TCommand>(Func<TCommand, TId> getId, Action<T, TCommand> action)
            where TCommand : class {
            _handlers.Add(
                new MapKey(typeof(TCommand), ExpectedState.Any),
                (aggregate, cmd) => action(aggregate, (TCommand) cmd)
            );

            _getId.TryAdd(typeof(TCommand), cmd => getId((TCommand) cmd));
        }

        public Task<Result<T, TState, TId>> HandleNew<TCommand>(TCommand command) where TCommand : class
            => Handle(command, ExpectedState.New);

        public Task<Result<T, TState, TId>> HandleExisting<TCommand>(TCommand command) where TCommand : class
            => Handle(command, ExpectedState.Existing);

        public Task<Result<T, TState, TId>> HandleAny<TCommand>(TCommand command) where TCommand : class
            => Handle(command, ExpectedState.Any);

        async Task<Result<T, TState, TId>> Handle<TCommand>(TCommand command, ExpectedState state)
            where TCommand : class {
            if (!_handlers.TryGetValue(new MapKey(typeof(TCommand), state), out var action)) {
                throw new Exceptions.CommandHandlerNotFound(typeof(TCommand));
            }

            var aggregate = state switch {
                ExpectedState.Any      => await TryLoad(),
                ExpectedState.Existing => await Load(),
                ExpectedState.New      => new T(),
                _                      => throw new ArgumentOutOfRangeException(nameof(state), state, null)
            };

            action(aggregate, command);

            await _store.Store(aggregate);

            return new OkResult<T, TState, TId>(aggregate.State, aggregate.Changes);

            Task<T> Load() {
                var id = _getId[typeof(TCommand)](command);
                return _store.Load<T>(id);
            }

            async Task<T> TryLoad() {
                try {
                    return await Load();
                }
                catch (Exceptions.AggregateNotFound<T>) {
                    return new T();
                }
            }
        }
    }

    record MapKey(Type Type, ExpectedState ExpectedState);

    class HandlersMap<T> : Dictionary<MapKey, Action<T, object>> { }

    class IdMap<T> : Dictionary<Type, Func<object, T>> { }

    enum ExpectedState {
        New,
        Existing,
        Any
    }
}