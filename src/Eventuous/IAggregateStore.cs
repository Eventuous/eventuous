using System.Threading;
using System.Threading.Tasks;

namespace Eventuous
{
    public interface IAggregateStore {
        Task Store<T>(T entity, CancellationToken cancellationToken) where T : Aggregate;

        Task<T> Load<T>(string id, CancellationToken cancellationToken) where T : Aggregate, new();

        Task<T> LoadState<T, TId>(StreamName stream, CancellationToken cancellationToken)
            where T : AggregateState<T, TId>, new() where TId : AggregateId;
    }
}
