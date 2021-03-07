using System.Threading.Tasks;

namespace Eventuous
{
    public interface IAggregateStore {
        Task Store<T>(T entity) where T : Aggregate;

        Task<T> Load<T>(string id) where T : Aggregate, new();
    }
}
