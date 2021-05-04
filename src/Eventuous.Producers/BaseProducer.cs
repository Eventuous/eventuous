using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Eventuous.Producers {
    public abstract class BaseProducer<TProduceOptions> : IEventProducer<TProduceOptions>
        where TProduceOptions : class {
        public abstract Task Initialize(CancellationToken cancellationToken = default);

        public abstract Task Shutdown(CancellationToken cancellationToken = default);

        public Task Produce<T>(
            T                 message,
            TProduceOptions?  options,
            CancellationToken cancellationToken = default
        ) where T : class
            => message is IEnumerable<object> collection
                ? ProduceMany(collection, options, cancellationToken)
                : ProduceOne(message, typeof(T), options, cancellationToken);

        public Task Produce(
            IEnumerable<object> messages,
            TProduceOptions?    options,
            CancellationToken   cancellationToken = default
        )
            => ProduceMany(messages, options, cancellationToken);

        protected abstract Task ProduceOne(
            object            message,
            Type              type,
            TProduceOptions?  options,
            CancellationToken cancellationToken
        );

        protected abstract Task ProduceMany(
            IEnumerable<object> messages,
            TProduceOptions?    options,
            CancellationToken   cancellationToken
        );
        

        public Task Produce<TMessage>(TMessage message, CancellationToken cancellationToken = default)
            where TMessage : class
            => Produce(message, null, cancellationToken);

        public Task Produce(IEnumerable<object> messages, CancellationToken cancellationToken = default)
            => Produce(messages, null, cancellationToken);
    }
}