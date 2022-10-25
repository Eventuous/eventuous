// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Threading.Channels;
using Eventuous.Subscriptions.Tools;

namespace Eventuous.Subscriptions.Channels;

public delegate ValueTask ProcessElement<T>(T element, CancellationToken cancellationToken);

static class ChannelExtensions {
    public static async Task Read<T>(
        this Channel<T>   channel,
        ProcessElement<T> process,
        CancellationToken cancellationToken
    ) {
        try {
            while (!cancellationToken.IsCancellationRequested) {
                var element = await channel.Reader.ReadAsync(cancellationToken).NoContext();
                await process(element, cancellationToken).NoContext();
            }
        }
        catch (OperationCanceledException) {
            // it's ok
        }
        catch (ChannelClosedException) {
            // ok, we are quitting
        }
    }

    public static ValueTask Write<T>(
        this Channel<T>   channel,
        T                 element,
        bool              throwOnFull,
        CancellationToken cancellationToken
    ) {
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
            await Task.WhenAll(incompleteReaders);
        }

        if (finalize == null) return;

        var token = new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token;
        await finalize(token);
    }
}
