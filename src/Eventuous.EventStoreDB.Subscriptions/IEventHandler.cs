using System.Threading.Tasks;

namespace Eventuous.EventStoreDB.Subscriptions {
    public interface IEventHandler {
        string SubscriptionGroup { get; }
        
        Task HandleEvent(object evt, long? position);
    }
}
