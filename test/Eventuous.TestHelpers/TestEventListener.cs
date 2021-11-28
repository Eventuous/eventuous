using System.Diagnostics.Tracing;

namespace Eventuous.TestHelpers;

public sealed class TestEventListener : EventListener {
    readonly ITestOutputHelper _outputHelper;
    readonly string[]          _prefixes;
    readonly List<EventSource> _eventSources = new();

    public TestEventListener(ITestOutputHelper outputHelper, params string[] prefixes) {
        _outputHelper = outputHelper;
        _prefixes     = prefixes.Length > 0 ? prefixes : new[] { "OpenTelemetry", "eventuous" };
    }

    protected override void OnEventSourceCreated(EventSource? eventSource) {
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
        string message;

        if (evt.Message != null && (evt.Payload?.Count ?? 0) > 0) {
            message = string.Format(evt.Message, evt.Payload.ToArray());
        }
        else {
            message = evt.Message;
        }

        _outputHelper.WriteLine(
            $"{evt.EventSource.Name} - EventId: [{evt.EventId}], EventName: [{evt.EventName}], Message: [{message}]"
        );
    }
    #nullable enable

    public override void Dispose() {
        foreach (var eventSource in _eventSources) {
            DisableEvents(eventSource);
        }

        base.Dispose();
    }
}