using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace Eventuous {
    [PublicAPI]
    public abstract class Aggregate {
        public IReadOnlyCollection<object> Changes => _changes.AsReadOnly();

        public void ClearChanges() => _changes.Clear();

        public int Version { get; protected set; } = -1;

        readonly List<object> _changes = new();

        public abstract void Load(IEnumerable<object?> events);
        
        public abstract void Fold(object evt);

        public abstract string GetId();

        protected void AddChange(object evt) => _changes.Add(evt);

        protected void EnsureDoesntExist() {
            if (Version > -1) throw new DomainException($"{GetType().Name} already exists: {GetId()}");
        }

        protected void EnsureExists() {
            if (Version == -1) throw new DomainException($"{GetType().Name} doesn't exist: {GetId()}");
        }
    }

    [PublicAPI]
    public abstract class Aggregate<T> : Aggregate where T : AggregateState<T>, new() {
        protected Aggregate() => State = new T();

        protected virtual (T PreviousState, T CurrentState) Apply(object evt) {
            AddChange(evt);
            var previous = State;
            State = State.When(evt);
            return (previous, State);
        }

        public override void Load(IEnumerable<object?> events) 
            => State = events.Where(x => x != null).Aggregate(new T(), Fold!);

        public override void Fold(object evt) => State = Fold(State, evt);

        T Fold(T state, object evt) {
            Version++;
            return state.When(evt);
        }

        public T State { get; private set; }
    }

    public abstract class Aggregate<T, TId> : Aggregate<T>
        where T : AggregateState<T, TId>, new()
        where TId : AggregateId {
        public override string GetId() => State.Id;
    }
}