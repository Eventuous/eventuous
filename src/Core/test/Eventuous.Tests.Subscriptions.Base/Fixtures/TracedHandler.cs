using System.Diagnostics;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.TestHelpers;

namespace Eventuous.Tests.Subscriptions.Base;

public class TracedHandler : BaseEventHandler {
    public List<RecordedTrace> Contexts { get; } = [];

    static readonly ValueTask<EventHandlingStatus> Success = new(EventHandlingStatus.Success);

    public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        Contexts.Add(new(Activity.Current?.TraceId, Activity.Current?.SpanId, Activity.Current?.ParentSpanId));

        return Success;
    }
}
