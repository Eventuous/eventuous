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
        Task<AppendEventsResult> AppendEvents(
            string                           stream,
            ExpectedStreamVersion            expectedVersion,
            IReadOnlyCollection<StreamEvent> events,
            CancellationToken                cancellationToken
        );

        Task<StreamEvent[]> ReadEvents(
            string             stream,
            StreamReadPosition start,
            int                count,
            CancellationToken  cancellationToken
        );

        Task<StreamEvent[]> ReadEventsBackwards(string stream, int count, CancellationToken cancellationToken);

        Task ReadStream(
            string              stream,
            StreamReadPosition  start,
            Action<StreamEvent> callback,
            CancellationToken   cancellationToken
        );
    }
}