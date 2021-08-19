using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Eventuous {
    /// <summary>
    /// Event Store is a place where events are stored. It is used by <see cref="AggregateStore"/> and
    /// <seealso cref="StateStore"/>
    /// </summary>
    [PublicAPI]
    public interface IEventStore {
        /// <summary>
        /// Append one or more events to a stream
        /// </summary>
        /// <param name="stream">Stream name</param>
        /// <param name="expectedVersion">Expected stream version (can be Any)</param>
        /// <param name="events">Collection of events to append</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Append result, which contains the global position of the last written event,
        /// as well as the next stream version</returns>
        Task<AppendEventsResult> AppendEvents(
            StreamName                       stream,
            ExpectedStreamVersion            expectedVersion,
            IReadOnlyCollection<StreamEvent> events,
            CancellationToken                cancellationToken
        );

        /// <summary>
        /// Read a fixed number of events from an existing stream to an array
        /// </summary>
        /// <param name="stream">Stream name</param>
        /// <param name="start">Where to start reading events</param>
        /// <param name="count">How many events to read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>An array with events retrieved from the stream</returns>
        Task<StreamEvent[]> ReadEvents(
            StreamName         stream,
            StreamReadPosition start,
            int                count,
            CancellationToken  cancellationToken
        );

        /// <summary>
        /// Read a number of events from a given stream, backwards (from the stream end)
        /// </summary>
        /// <param name="stream">Stream name</param>
        /// <param name="count">How many events to read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>An array with events retrieved from the stream</returns>
        Task<StreamEvent[]> ReadEventsBackwards(
            StreamName        stream,
            int               count,
            CancellationToken cancellationToken
        );

        /// <summary>
        /// Read events from a stream asynchronously, calling a given function for each retrieved event
        /// </summary>
        /// <param name="stream">Stream name</param>
        /// <param name="start">Where to start reading events</param>
        /// <param name="callback">A function to be called for retrieved each event</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns></returns>
        Task ReadStream(
            StreamName          stream,
            StreamReadPosition  start,
            Action<StreamEvent> callback,
            CancellationToken   cancellationToken
        );

        /// <summary>
        /// Truncate a stream at a given position
        /// </summary>
        /// <param name="stream">Stream name</param>
        /// <param name="truncatePosition">Where to truncate the stream</param>
        /// <param name="expectedVersion">Expected stream version (could be Any)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns></returns>
        Task TruncateStream(
            StreamName             stream,
            StreamTruncatePosition truncatePosition,
            ExpectedStreamVersion  expectedVersion,
            CancellationToken      cancellationToken
        );

        /// <summary>
        /// Delete a stream
        /// </summary>
        /// <param name="stream">Stream name</param>
        /// <param name="expectedVersion">Expected stream version (could be Any)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns></returns>
        Task DeleteStream(
            StreamName            stream,
            ExpectedStreamVersion expectedVersion,
            CancellationToken     cancellationToken
        );
    }
}
