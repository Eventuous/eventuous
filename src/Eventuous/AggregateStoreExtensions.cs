using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Eventuous {
    [PublicAPI]
    public static class AggregateStoreExtensions {
        public static Task<T> Load<T, TState, TId>(
            this IAggregateStore store,
            TId                  id,
            CancellationToken    cancellationToken
        )
            where T : Aggregate<TState, TId>, new()
            where TState : AggregateState<TState, TId>, new()
            where TId : AggregateId
            => store.Load<T>(id.ToString(), cancellationToken);
    }
}