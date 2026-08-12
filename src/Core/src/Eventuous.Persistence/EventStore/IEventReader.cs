// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public interface IEventReader {
    /// <summary>
    /// Read a fixed number of events from an existing stream as an async enumerable.
    /// Throws <see cref="StreamNotFound"/> if the stream does not exist.
    /// Implementations either stream events as they arrive from the store, or buffer up to <paramref name="count"/>
    /// events before yielding, so memory usage can grow with <paramref name="count"/>. To read a whole stream,
    /// use <see cref="StoreFunctions.ReadStreamToEnd"/>, which reads in pages, instead of passing
    /// <see cref="int.MaxValue"/> as the count.
    /// </summary>
    /// <param name="stream">Stream name</param>
    /// <param name="start">Where to start reading events</param>
    /// <param name="count">How many events to read</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An async enumerable of events retrieved from the stream</returns>
    IAsyncEnumerable<StreamEvent> ReadEvents(StreamName stream, StreamReadPosition start, int count, CancellationToken cancellationToken);

    /// <summary>
    /// Read a number of events from a given stream, backwards (from the stream end).
    /// Throws <see cref="StreamNotFound"/> if the stream does not exist.
    /// Implementations either stream events as they arrive from the store, or buffer up to <paramref name="count"/>
    /// events before yielding, so memory usage can grow with <paramref name="count"/>.
    /// </summary>
    /// <param name="stream">Stream name</param>
    /// <param name="start">Where to start reading events</param>
    /// <param name="count">How many events to read</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An async enumerable of events retrieved from the stream</returns>
    IAsyncEnumerable<StreamEvent> ReadEventsBackwards(StreamName stream, StreamReadPosition start, int count, CancellationToken cancellationToken);
}
