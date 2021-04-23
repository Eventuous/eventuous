using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Eventuous.Producers {
    [PublicAPI]
    public interface IEventProducer {
        /// <summary>
        /// Produce a message of type <see cref="T"/>. The type is used to look up the type name
        /// in the <seealso cref="TypeMap"/>.
        /// </summary>
        /// <param name="message">Message to produce</param>
        /// <param name="cancellationToken"></param>
        /// <typeparam name="T">Message typ</typeparam>
        /// <returns></returns>
        Task Produce<T>(T message, CancellationToken cancellationToken =default) where T : class;

        /// <summary>
        /// Produce a batch of messages, use the message type returned by message.GetType,
        /// then look it up in the <seealso cref="TypeMap"/>.
        /// </summary>
        /// <param name="messages"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task Produce(IEnumerable<object> messages, CancellationToken cancellationToken = default);
    }
}