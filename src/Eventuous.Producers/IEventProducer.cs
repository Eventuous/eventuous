using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Eventuous.Producers {
    [PublicAPI]
    public interface IEventProducer<in TProduceOptions> where TProduceOptions : class {
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
        /// <param name="message">Message to produce</param>
        /// <param name="options"></param>
        /// <param name="cancellationToken"></param>
        /// <typeparam name="TMessage">Message typ</typeparam>
        /// <returns></returns>
        Task Produce<TMessage>(
            TMessage          message,
            TProduceOptions?  options           = null,
            CancellationToken cancellationToken = default
        )
            where TMessage : class;

        /// <summary>
        /// Produce a batch of messages, use the message type returned by message.GetType,
        /// then look it up in the <seealso cref="TypeMap"/>.
        /// </summary>
        /// <param name="messages"></param>
        /// <param name="options"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task Produce(
            IEnumerable<object> messages,
            TProduceOptions?    options           = null,
            CancellationToken   cancellationToken = default
        );
    }
}