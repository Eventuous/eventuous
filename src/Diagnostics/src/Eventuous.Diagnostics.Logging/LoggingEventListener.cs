using System.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;

namespace Eventuous.Diagnostics.Logging;

public sealed class LoggingEventListener : EventListener {
    readonly List<EventSource> _eventSources = new();
    readonly ILogger           _log;

    public LoggingEventListener(ILoggerFactory loggerFactory)
        => _log = loggerFactory.CreateLogger(DiagnosticName.BaseName);

    protected override void OnEventSourceCreated(EventSource eventSource) {
        if (eventSource.Name.StartsWith(DiagnosticName.BaseName)) {
            _eventSources.Add(eventSource);
            EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All);
        }

        base.OnEventSourceCreated(eventSource);
    }

    protected override void OnEventWritten(EventWrittenEventArgs evt) {
        if (evt.Message == null) return;

        var level = evt.Level switch {
            EventLevel.Critical      => LogLevel.Critical,
            EventLevel.Error         => LogLevel.Error,
            EventLevel.Informational => LogLevel.Information,
            EventLevel.Warning       => LogLevel.Warning,
            EventLevel.Verbose       => LogLevel.Debug,
            _                        => LogLevel.Information
        };

#pragma warning disable CA2254
        if (evt.Payload != null)
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            _log.Log(level, evt.Message, evt.Payload.ToArray());
        else
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            _log.Log(level, evt.Message);
#pragma warning restore CA2254
    }

    public override void Dispose() {
        foreach (var eventSource in _eventSources) {
            DisableEvents(eventSource);
        }

        base.Dispose();
    }
}