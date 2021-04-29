using System.Threading;
using System.Threading.Tasks;

namespace Eventuous.Subscriptions {
    public interface IEventHandler {
        string SubscriptionId { get; }
        
        Task HandleEvent(object evt, long? position, CancellationToken cancellationToken);
    }
}
