using System.Diagnostics.Tracing;

namespace Eventuous.TestHelpers;

public sealed class TestEventListener(ITestOutputHelper outputHelper, Action<EventWrittenEventArgs>? act = null, params string[] prefixes) : EventListener {
    readonly string[]          _prefixes     = prefixes.Length > 0 ? prefixes : new[] { "OpenTelemetry", "eventuous" };
    readonly List<EventSource> _eventSources = new();

    protected override void OnEventSourceCreated(EventSource? eventSource) {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (_prefixes == null || eventSource?.Name == null) {
            return;
        }

        if (_prefixes.Any(x => eventSource.Name.StartsWith(x))) {
            _eventSources.Add(eventSource);
            EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All);
        }

        base.OnEventSourceCreated(eventSource);
    }

#nullable disable
    protected override void OnEventWritten(EventWrittenEventArgs evt) {
        var message = evt.Message != null && (evt.Payload?.Count ?? 0) > 0 ? string.Format(evt.Message, evt.Payload.ToArray()) : evt.Message;
        outputHelper.WriteLine($"{evt.EventSource.Name} - EventId: [{evt.EventId}], EventName: [{evt.EventName}], Message: [{message}]");
        act?.Invoke(evt);
    }
#nullable enable

    public override void Dispose() {
        foreach (var eventSource in _eventSources) {
            DisableEvents(eventSource);
        }

        base.Dispose();
    }
}
