using System.Threading.Channels;

namespace Eventuous.Subscriptions.Channels;

public delegate ValueTask ProcessElement<T>(T element, CancellationToken cancellationToken);

static class ChannelExtensions {
    public static async Task Read<T>(
        this Channel<T>   channel,
        ProcessElement<T> process,
        CancellationToken cancellationToken
    ) {
        try {
            while (!cancellationToken.IsCancellationRequested && !channel.Reader.Completion.IsCompleted) {
                var element = await channel.Reader.ReadAsync(cancellationToken).NoContext();
                await process(element, cancellationToken).NoContext();
            }
        }
        catch (OperationCanceledException) {
            // it's ok
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
        channel.Writer.Complete();
        cts.CancelAfter(TimeSpan.FromSeconds(1));

        while (!readers.All(x => x.IsCompleted) && !readers.All(x => x.IsCanceled)) {
            await Task.Delay(10);
        }

        if (!cts.IsCancellationRequested && finalize != null) {
            await finalize(cts.Token);
        }
    }
}