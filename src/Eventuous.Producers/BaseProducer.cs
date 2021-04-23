using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Eventuous.Producers {
    public abstract class BaseProducer : IEventProducer {
        public Task Produce<T>(T message, CancellationToken cancellationToken = default) where T : class
            => message is IEnumerable<object> collection 
                ? ProduceMany(collection, cancellationToken)
                : ProduceOne(message, typeof(T), cancellationToken);

        public Task Produce(IEnumerable<object> messages, CancellationToken cancellationToken = default)
            => ProduceMany(messages, cancellationToken);

        protected abstract Task ProduceOne(object message, Type type, CancellationToken cancellationToken);

        protected abstract Task ProduceMany(IEnumerable<object> messages, CancellationToken cancellationToken);
    }
}