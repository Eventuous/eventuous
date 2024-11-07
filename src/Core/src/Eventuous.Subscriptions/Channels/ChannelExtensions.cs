// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Eventuous.Subscriptions.Channels;

public delegate ValueTask ProcessElement<in T>(T element, CancellationToken cancellationToken);

static class ChannelExtensions {
    public static async Task Read<T>(this Channel<T> channel, ProcessElement<T> process, CancellationToken cancellationToken) {
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

    public static async Task ReadBatches<T>(
            this Channel<T>     channel,
            ProcessElement<T[]> process,
            int                 maxCount,
            TimeSpan            maxTime,
            CancellationToken   cancellationToken
        ) {
        await foreach (var batch in channel.Reader.ReadAllBatches(maxCount, maxTime, cancellationToken).NoContext(cancellationToken)) {
            await process(batch, cancellationToken).NoContext();
        }
    }

    public static ValueTask Write<T>(this Channel<T> channel, T element, bool throwOnFull, CancellationToken cancellationToken) {
        return throwOnFull ? WriteOrThrow() : channel.Writer.WriteAsync(element, cancellationToken);

        ValueTask WriteOrThrow() {
            if (!channel.Writer.TryWrite(element)) {
                throw new ChannelFullException();
            }

            return default;
        }
    }

    public static async ValueTask Stop<T>(
            this Channel<T>                     channel,
            CancellationTokenSource             cts,
            Task[]                              readers,
            Func<CancellationToken, ValueTask>? finalize = null
        ) {
        channel.Writer.TryComplete();

        var incompleteReaders = readers.Where(r => !r.IsCompleted).ToArray();

        if (readers.Length > 0) {
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            await Task.WhenAll(incompleteReaders).NoContext();
        }

        if (finalize == null) return;

        using var ts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await finalize(ts.Token).NoContext();
    }

    public static async IAsyncEnumerable<T[]> ReadAllBatches<T>(
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

                yield return buffer.ToArray();

                buffer.Clear();

                if (!timerCts.TryReset()) {
                    timerCts.Dispose();
                    timerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                }
            }

            // Emit what's left before throwing exceptions.
            if (buffer.Count > 0) yield return buffer.ToArray();

            cancellationToken.ThrowIfCancellationRequested();

            // Propagate possible failure of the channel.
            if (source.Completion.IsCompleted)
                await source.Completion.ConfigureAwait(false);
        } finally { timerCts.Dispose(); }
    }
}
