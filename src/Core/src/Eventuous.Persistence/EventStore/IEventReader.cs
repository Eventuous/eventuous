// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public interface IEventReader {
    /// <summary>
    /// Read a fixed number of events from an existing stream to an array
    /// </summary>
    /// <param name="stream">Stream name</param>
    /// <param name="start">Where to start reading events</param>
    /// <param name="count">How many events to read</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An array with events retrieved from the stream</returns>
    Task<StreamEvent[]> ReadEvents(StreamName stream, StreamReadPosition start, int count, CancellationToken cancellationToken);

    /// <summary>
    /// Read a number of events from a given stream, backwards (from the stream end)
    /// </summary>
    /// <param name="stream">Stream name</param>
    /// <param name="start">Where to start reading events</param>
    /// <param name="count">How many events to read</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An array with events retrieved from the stream</returns>
    Task<StreamEvent[]> ReadEventsBackwards(StreamName stream, StreamReadPosition start,int count, CancellationToken cancellationToken);
}
