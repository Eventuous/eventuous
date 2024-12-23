// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Persistence;

static class WriterExtensions {
    public static Task<AppendEventsResult> Store(this IEventWriter writer, ProposedAppend append, AmendEvent? amendEvent, CancellationToken cancellationToken)
        => writer.Store(append.StreamName, append.ExpectedVersion, append.Events, amendEvent, cancellationToken);
}
