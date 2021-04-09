using System.Threading.Tasks;

namespace Eventuous.EventStoreDB.Subscriptions {
    public interface IEventHandler {
        string SubscriptionId { get; }
        
        Task HandleEvent(object evt, long? position);
    }
}
