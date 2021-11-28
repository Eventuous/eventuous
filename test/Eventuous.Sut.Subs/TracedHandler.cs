using System.Diagnostics;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.TestHelpers;

namespace Eventuous.Sut.Subs;

public class TracedHandler : BaseEventHandler {
    public List<RecordedTrace> Contexts { get; } = new();

    static readonly ValueTask<EventHandlingStatus> Success = new(EventHandlingStatus.Success);
    
    public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        Contexts.Add(
            new RecordedTrace(
                Activity.Current?.TraceId,
                Activity.Current?.SpanId,
                Activity.Current?.ParentSpanId
            )
        );

        return Success;
    }
}