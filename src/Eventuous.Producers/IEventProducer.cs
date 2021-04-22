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
        /// <typeparam name="T">Message typ</typeparam>
        /// <returns></returns>
        Task Produce<T>(T message) where T : class;

        /// <summary>
        /// Produce a message, use the message type returned by message.GetType, then
        /// look it up in the <seealso cref="TypeMap"/>.
        /// </summary>
        /// <param name="message">Message to produce</param>
        /// <returns></returns>
        Task Produce(object message);
    }
}