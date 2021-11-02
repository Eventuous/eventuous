using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace Eventuous.Tests.EventStore.Fixtures;

public static class Logging {
    public static ILoggerFactory GetLoggerFactory(ITestOutputHelper outputHelper)
        => LoggerFactory.Create(
            builder => builder
                .SetMinimumLevel(LogLevel.Debug)
                .AddXunit(outputHelper)
        );

    public class EventuousEventListener : EventListener {
        readonly ITestOutputHelper _outputHelper;
        readonly         string[]          _prefixes     = {"OpenTelemetry", "eventuous"};
        readonly         List<EventSource> _eventSources = new();

        public EventuousEventListener(ITestOutputHelper outputHelper) {
            _outputHelper = outputHelper;
        }

        protected override void OnEventSourceCreated(EventSource? eventSource) {
            if (eventSource?.Name == null) {
                return;
            }
            
            if (_prefixes.Any(x => eventSource.Name.StartsWith(x))) {
                _eventSources.Add(eventSource);
                EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All);
            }

            base.OnEventSourceCreated(eventSource);
        }

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

        public override void Dispose() {
            foreach (var eventSource in this._eventSources) {
                DisableEvents(eventSource);
            }

            base.Dispose();
        }
    }
}
