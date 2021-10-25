using System.Threading.Channels;

namespace Eventuous.Subscriptions.Channels;

public class ChannelWorker<T> {
    readonly Channel<T>              _channel;
    readonly bool                    _throwOnFull;
    readonly CancellationTokenSource _cts;
    readonly Task                    _readerTask;

    /// <summary>
    /// Creates a new instance of the channel worker, starts a task for background reads
    /// </summary>
    /// <param name="channel">Channel to use for writes and reads</param>
    /// <param name="process">Function to process each element the worker reads from the channel</param>
    /// <param name="throwOnFull">Throw if the channel is full to prevent partition blocks</param>
    public ChannelWorker(
        Channel<T>                            channel,
        Func<T, CancellationToken, ValueTask> process,
        bool                                  throwOnFull = false
    ) {
        _channel     = channel;
        _throwOnFull = throwOnFull;
        _cts         = new CancellationTokenSource();
        _readerTask  = Task.Run(() => Read(_cts.Token));

        async Task Read(CancellationToken cancellationToken) {
            try {
                while (!cancellationToken.IsCancellationRequested && !channel.Reader.Completion.IsCompleted) {
                    var element = await channel.Reader.ReadAsync(cancellationToken);
                    await process(element, cancellationToken);
                }
            }
            catch (OperationCanceledException) {
                // it's ok
            }
        }
    }

    public ValueTask Write(T element, CancellationToken cancellationToken) {
        return _throwOnFull ? WriteOrThrow() : _channel.Writer.WriteAsync(element, cancellationToken);

        ValueTask WriteOrThrow() {
            if (!_channel.Writer.TryWrite(element)) {
                throw new ChannelFullException();
            }

            return default;
        }
    }

    public async ValueTask Stop(Func<CancellationToken, ValueTask>? finalize = null) {
        _channel.Writer.Complete();
        _cts.CancelAfter(TimeSpan.FromSeconds(1));

        while (!_readerTask.IsCompleted && !_readerTask.IsCanceled) {
            await Task.Delay(10);
        }

        if (!_cts.IsCancellationRequested && finalize != null) {
            await finalize(_cts.Token);
        }
    }
}