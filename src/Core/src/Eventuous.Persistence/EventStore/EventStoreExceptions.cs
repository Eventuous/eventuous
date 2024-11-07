// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public abstract class EventStoreException : Exception {
    protected EventStoreException(string message, Exception inner) : base(message, inner) { }

    protected EventStoreException(string message) : base(message) { }
}

public class StreamNotFound(string stream) : EventStoreException($"Stream {stream} does not exist");

public class AppendToStreamException(string stream, Exception inner) : EventStoreException($"Unable to append events to {stream}: {inner.Message}", inner);

public class ReadFromStreamException(string stream, Exception inner) : EventStoreException($"Unable to read events from {stream}: {inner.Message}", inner);

public class DeleteStreamException(string stream, Exception inner) : EventStoreException($"Unable to delete stream {stream}: {inner.Message}", inner);

public class TruncateStreamException(string stream, Exception inner) : EventStoreException($"Unable to truncate stream {stream}: {inner.Message}", inner);