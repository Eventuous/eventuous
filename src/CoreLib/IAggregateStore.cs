using System.Threading.Tasks;

namespace CoreLib
{
    public interface IAggregateStore {
        Task Store<T>(T entity) where T : Aggregate;

        Task<T> Load<T>(string id) where T : Aggregate, new();
    }
}
