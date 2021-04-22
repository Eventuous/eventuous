using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Eventuous.Producers {
    [PublicAPI]
    public interface IEventProducer {
        Task Produce<T>(T message) where T : class;

        Task Produce(object message);
    }
}