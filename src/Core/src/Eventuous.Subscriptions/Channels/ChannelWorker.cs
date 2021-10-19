using System.Threading.Channels;

namespace Eventuous.Subscriptions.Channels;

public class ChannelWorker<T> {
    readonly Channel<T>              _channel;
    readonly CancellationTokenSource _cts;

    public ChannelWorker(Channel<T> channel, Func<T, CancellationToken, ValueTask> process) {
        _channel = channel;
        _cts     = new CancellationTokenSource();
        Task.Run(() => Read(_cts.Token));

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

    public ValueTask Write(T element, CancellationToken cancellationToken)
        => _channel.Writer.WriteAsync(element, cancellationToken);

    public async ValueTask Stop(Func<CancellationToken, ValueTask>? finalize = null) {
        _channel.Writer.Complete();
        _cts.CancelAfter(TimeSpan.FromSeconds(1));

        while (_channel.Reader.Completion.IsCompleted && !_cts.IsCancellationRequested) {
            await Task.Delay(10);
        }

        if (!_cts.IsCancellationRequested && finalize != null) {
            await finalize(_cts.Token);
        }
    }
}