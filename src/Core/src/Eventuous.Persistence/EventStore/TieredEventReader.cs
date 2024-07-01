// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public class TieredEventReader(IEventReader hotReader, IEventReader archiveReader) : IEventReader {
    public Task<StreamEvent[]> ReadEvents(StreamName stream, StreamReadPosition start, int count, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<StreamEvent[]> ReadEventsBackwards(StreamName stream, int count, CancellationToken cancellationToken) => throw new NotImplementedException();
}
