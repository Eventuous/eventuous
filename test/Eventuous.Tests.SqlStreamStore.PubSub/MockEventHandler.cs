using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Eventuous.Subscriptions;
using Eventuous.Producers.SqlStreamStore;
using Eventuous.Subscriptions.SqlStreamStore;

namespace Eventuous.Tests.SqlStreamStore.PubSub
{
    public class MockEventHandler : IEventHandler
    {
        public List<object> ReceivedEvents = new List<object>(); 
        public string SubscriptionId { get; }
        public MockEventHandler(string subscriptionId) => SubscriptionId = subscriptionId;
        public Task HandleEvent(object evt, long? position, CancellationToken cancellationToken) {
            ReceivedEvents.Add(evt);
            return Task.CompletedTask;
        }
    }
}