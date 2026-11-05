using System.Diagnostics.Metrics;
using Eventuous.Diagnostics;

namespace Eventuous.Tests.Diagnostics;

[NotInParallel]
public class TracedCommandServiceMetricsTests {
    [Test]
    public async Task ShouldMeasureEveryTracedService() {
        var services = new List<object?>();

        using var meterListener = new MeterListener {
            InstrumentPublished = (instrument, listener) => {
                if (instrument.Name == CommandServiceMetrics.ProcessingRateName) listener.EnableMeasurementEvents(instrument);
            }
        };

        meterListener.SetMeasurementEventCallback<double>(
            (_, _, tags, _) => {
                foreach (var tag in tags) {
                    if (tag.Key != "command-service") continue;

                    lock (services) services.Add(tag.Value);
                }
            }
        );
        meterListener.Start();

        // The metrics listener has to exist before the services: each service used to announce its own diagnostic
        // listener, and the metrics listener only kept the last one it saw.
        using var metrics = new CommandServiceMetrics();

        var first  = TracedCommandService<FirstState>.Trace(new FirstService());
        var second = TracedCommandService<SecondState>.Trace(new SecondService());

        await first.Handle(new TestCommand(), CancellationToken.None);
        await second.Handle(new TestCommand(), CancellationToken.None);

        await Assert.That(services).Contains(nameof(FirstService));
        await Assert.That(services).Contains(nameof(SecondService));
    }

    record TestCommand;

    record FirstState : State<FirstState>;

    record SecondState : State<SecondState>;

    class FirstService : ICommandService<FirstState> {
        public Task<Result<FirstState>> Handle<TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : class
            => Task.FromResult(Result<FirstState>.FromSuccess(new(), [], 0));
    }

    class SecondService : ICommandService<SecondState> {
        public Task<Result<SecondState>> Handle<TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : class
            => Task.FromResult(Result<SecondState>.FromSuccess(new(), [], 0));
    }
}
