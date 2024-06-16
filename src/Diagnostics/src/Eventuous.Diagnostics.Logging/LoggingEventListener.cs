using System.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;

namespace Eventuous.Diagnostics.Logging;

public sealed class LoggingEventListener : EventListener {
    readonly string            _prefix       = DiagnosticName.BaseName;
    readonly List<EventSource> _eventSources = [];
    readonly ILogger           _log;
    readonly EventLevel        _level;
    readonly EventKeywords     _keywords;

    public LoggingEventListener(ILoggerFactory loggerFactory,
        string?                                prefix   = null,
        EventLevel                             level    = EventLevel.Verbose,
        EventKeywords                          keywords = EventKeywords.All) {
        if (prefix != null) _prefix = prefix;
        _log      = loggerFactory.CreateLogger(DiagnosticName.BaseName);
        _level    = level;
        _keywords = keywords;
    }

    protected override void OnEventSourceCreated(EventSource? eventSource) {
        if (eventSource?.Name == null) return;

        if (eventSource.Name.StartsWith(_prefix)) {
            _eventSources.Add(eventSource);
            EnableEvents(eventSource, _level, _keywords);
        }

        base.OnEventSourceCreated(eventSource);
    }

    protected override void OnEventWritten(EventWrittenEventArgs evt) {
        if (evt.Message == null) return;

        var level = evt.Level switch {
            EventLevel.Critical => LogLevel.Critical,
            EventLevel.Error => LogLevel.Error,
            EventLevel.Informational => LogLevel.Information,
            EventLevel.Warning => LogLevel.Warning,
            EventLevel.Verbose => LogLevel.Debug,
            _ => LogLevel.Information
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