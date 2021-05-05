using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Eventuous.Producers {
    [PublicAPI]
    public interface IEventProducer {
        /// <summary>
        /// Initializes the producer, creating necessary resources if needed
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task Initialize(CancellationToken cancellationToken = default);

        /// <summary>
        /// Shuts down the producer, ensuring all the locally queues messages are sent to the server.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task Shutdown(CancellationToken cancellationToken = default);

        /// <summary>
        /// Produce a message of type <see cref="TMessage"/>. The type is used to look up the type name
        /// in the <seealso cref="TypeMap"/>.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="message">Message to produce</param>
        /// <param name="cancellationToken"></param>
        /// <typeparam name="TMessage">Message typ</typeparam>
        /// <returns></returns>
        Task Produce<TMessage>(
            string            stream,
            TMessage          message,
            CancellationToken cancellationToken = default
        )
            where TMessage : class;

        /// <summary>
        /// Produce a batch of messages, use the message type returned by message.GetType,
        /// then look it up in the <seealso cref="TypeMap"/>.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="messages"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task Produce(
            string              stream,
            IEnumerable<object> messages,
            CancellationToken   cancellationToken = default
        );
    }

    [PublicAPI]
    public interface IEventProducer<in TProduceOptions> : IEventProducer where TProduceOptions : class {
        /// <summary>
        /// Produce a message of type <see cref="TMessage"/>. The type is used to look up the type name
        /// in the <seealso cref="TypeMap"/>.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="message">Message to produce</param>
        /// <param name="options"></param>
        /// <param name="cancellationToken"></param>
        /// <typeparam name="TMessage">Message typ</typeparam>
        /// <returns></returns>
        Task Produce<TMessage>(
            string            stream,
            TMessage          message,
            TProduceOptions?  options,
            CancellationToken cancellationToken = default
        )
            where TMessage : class;

        /// <summary>
        /// Produce a batch of messages, use the message type returned by message.GetType,
        /// then look it up in the <seealso cref="TypeMap"/>.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="messages"></param>
        /// <param name="options"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task Produce(
            string              stream,
            IEnumerable<object> messages,
            TProduceOptions?    options,
            CancellationToken   cancellationToken = default
        );
    }
}