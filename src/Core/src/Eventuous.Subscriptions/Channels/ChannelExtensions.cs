// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Eventuous.Subscriptions.Channels;

public delegate ValueTask ProcessElement<in T>(T element, CancellationToken cancellationToken);

static class ChannelExtensions {
    extension<T>(Channel<T> channel) {
        public async Task Read(ProcessElement<T> process, CancellationToken cancellationToken) {
            try {
                while (!cancellationToken.IsCancellationRequested) {
                    var element = await channel.Reader.ReadAsync(cancellationToken).NoContext();
                    await process(element, cancellationToken).NoContext();
                }
            } catch (OperationCanceledException) {
                // it's ok
            } catch (ChannelClosedException) {
                // ok, we are quitting
            }
        }

        public async Task ReadBatches(
                ProcessElement<IReadOnlyList<T>> process,
                int                 maxCount,
                TimeSpan            maxTime,
                CancellationToken   cancellationToken
            ) {
            await foreach (var batch in channel.Reader.ReadAllBatches(maxCount, maxTime, cancellationToken).NoContext(cancellationToken)) {
                await process(batch, cancellationToken).NoContext();
            }
        }

        public async ValueTask Stop(
                CancellationTokenSource             cts,
                Task[]                              readers,
                Func<CancellationToken, ValueTask>? finalize = null
            ) {
            channel.Writer.TryComplete();

            var incompleteReaders = readers.Where(r => !r.IsCompleted).ToArray();

            try {
                // Only incomplete readers need a deadline; arming one for readers already done would cancel
                // a drain that's already finished.
                if (incompleteReaders.Length > 0) {
                    cts.CancelAfter(TimeSpan.FromSeconds(10));
                    await Task.WhenAll(incompleteReaders).NoContext();
                }
            } finally {
                // In a finally: finalize is the only forced checkpoint commit, and matters most when the
                // drain above times out (a batch reader rethrows that cancellation). The reader's own failure
                // still propagates afterwards, so a broken shutdown isn't traded away for the flush.
                if (finalize != null) {
                    using var ts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await finalize(ts.Token).NoContext();
                }
            }
        }
    }

    static async IAsyncEnumerable<IReadOnlyList<T>> ReadAllBatches<T>(
            this ChannelReader<T>                      source,
            int                                        batchSize,
            TimeSpan                                   timeSpan,
            [EnumeratorCancellation] CancellationToken cancellationToken
        ) {
        Ensure.NotNull(source);
        var timerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try {
            List<T> buffer = [];

            while (true) {
                var token = buffer.Count == 0 ? cancellationToken : timerCts.Token;

                (T Value, bool HasValue) item;

                try {
                    item = (await source.ReadAsync(token).NoContext(), true);
                } catch (ChannelClosedException) {
                    break;
                } catch (OperationCanceledException) {
                    if (cancellationToken.IsCancellationRequested) break;

                    item = default;
                }

                if (buffer.Count == 0) timerCts.CancelAfter(timeSpan);

                if (item.HasValue) {
                    buffer.Add(item.Value!);

                    if (buffer.Count < batchSize) continue;
                }

                yield return buffer.AsReadOnly();

                buffer.Clear();

                if (!timerCts.TryReset()) {
                    timerCts.Dispose();
                    timerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                }
            }

            // Emit what's left before throwing exceptions.
            if (buffer.Count > 0) yield return buffer.AsReadOnly();

            cancellationToken.ThrowIfCancellationRequested();

            // Propagate possible failure of the channel.
            if (source.Completion.IsCompleted)
                await source.Completion.NoContext();
        } finally { timerCts.Dispose(); }
    }
}
