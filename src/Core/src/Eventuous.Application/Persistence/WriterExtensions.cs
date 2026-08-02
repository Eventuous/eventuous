// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Persistence;

static class WriterExtensions {
    extension(IEventWriter writer) {
        public async Task<AppendEventsResult> Store(ProposedAppend append, AmendEvent? amendEvent, CancellationToken cancellationToken) {
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

        public async Task<AppendEventsResult[]> Store(
                IReadOnlyCollection<ProposedAppend> appends,
                AmendEvent?                         amendEvent,
                CancellationToken                   cancellationToken
            ) {
            if (appends.Count == 0) return [];

            var streamAppends = appends.Select(a => {
                        Ensure.NotNull(a.Events);

                        return new NewStreamAppend(
                            a.StreamName,
                            a.ExpectedVersion,
                            a.Events.Select(evt => ToStreamEvent(evt, amendEvent)).ToArray()
                        );
                    }
                )
                .ToArray();

            try {
                return await writer.AppendEvents(streamAppends, cancellationToken).NoContext();
            } catch (Exception e) {
                throw e.InnerException?.Message.Contains("WrongExpectedVersion") == true
                    ? new OptimisticConcurrencyException(new(string.Join(", ", appends.Select(a => a.StreamName.ToString()))), e)
                    : e;
            }

            static NewStreamEvent ToStreamEvent(ProposedEvent evt, AmendEvent? amendEvent) {
                var streamEvent = new NewStreamEvent(Guid.NewGuid(), evt.Data, evt.Metadata);

                return amendEvent?.Invoke(streamEvent) ?? streamEvent;
            }
        }
    }
}
