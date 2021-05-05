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
            string            stream,
            T                 message,
            TProduceOptions?  options,
            CancellationToken cancellationToken = default
        ) where T : class
            => message is IEnumerable<object> collection
                ? ProduceMany(stream, collection, options, cancellationToken)
                : ProduceOne(stream, message, typeof(T), options, cancellationToken);

        public Task Produce(
            string            stream,
            IEnumerable<object> messages,
            TProduceOptions?    options,
            CancellationToken   cancellationToken = default
        )
            => ProduceMany(stream, messages, options, cancellationToken);

        protected abstract Task ProduceOne(
            string            stream,
            object            message,
            Type              type,
            TProduceOptions?  options,
            CancellationToken cancellationToken
        );

        protected abstract Task ProduceMany(
            string            stream,
            IEnumerable<object> messages,
            TProduceOptions?    options,
            CancellationToken   cancellationToken
        );
        

        public Task Produce<TMessage>(string stream, TMessage message, CancellationToken cancellationToken = default)
            where TMessage : class
            => Produce(stream, message, null, cancellationToken);

        public Task Produce(string stream, IEnumerable<object> messages, CancellationToken cancellationToken = default)
            => Produce(stream, messages, null, cancellationToken);
    }
}