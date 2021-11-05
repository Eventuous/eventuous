using System.Diagnostics;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.TestHelpers;

namespace Eventuous.Sut.Subs;

public class TracedHandler : IEventHandler {
    public List<RecordedTrace> Contexts { get; } = new();

    public ValueTask HandleEvent(IMessageConsumeContext context, CancellationToken cancellationToken) {
        Contexts.Add(
            new RecordedTrace(
                Activity.Current?.TraceId,
                Activity.Current?.SpanId,
                Activity.Current?.ParentSpanId
            )
        );

        return default;
    }
}