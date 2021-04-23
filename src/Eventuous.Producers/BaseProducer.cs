using System;
using System.Threading.Tasks;

namespace Eventuous.Producers {
    public abstract class BaseProducer : IEventProducer {
        public Task Produce<T>(T message) where T : class => Produce(message, typeof(T));

        public Task Produce(object message) => Produce(message, message.GetType());
        
        protected abstract Task Produce(object message, Type type);
    }
}