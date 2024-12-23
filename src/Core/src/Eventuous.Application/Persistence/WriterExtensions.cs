// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Persistence;

static class WriterExtensions {
    public static async Task<AppendEventsResult> Store(this IEventWriter writer, ProposedAppend append, AmendEvent? amendEvent, CancellationToken cancellationToken) {
        Ensure.NotNull(append.Events);

        if (append.Events.Length == 0) return AppendEventsResult.NoOp;

        try {
            return await writer.AppendEvents(
                    append.StreamName,
                    append.ExpectedVersion,
                    append.Events.Select(ToStreamEvent).ToArray(),
                    cancellationToken
                )
                .NoContext();
        } catch (Exception e) {
            throw e.InnerException?.Message.Contains("WrongExpectedVersion") == true
                ? new OptimisticConcurrencyException(append.StreamName, e)
                : e;
        }

        NewStreamEvent ToStreamEvent(ProposedEvent evt) {
            var streamEvent = new NewStreamEvent(Guid.NewGuid(), evt.Data, evt.Metadata);

            return amendEvent?.Invoke(streamEvent) ?? streamEvent;
        }
    }
}
