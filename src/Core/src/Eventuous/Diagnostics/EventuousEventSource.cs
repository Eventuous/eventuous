using System.Diagnostics.Tracing;
// ReSharper disable MemberCanBePrivate.Global

namespace Eventuous.Diagnostics;

public static class DiagnosticName {
    public const string BaseName = "eventuous";
}

[EventSource(Name = DiagnosticName.BaseName)]
class EventuousEventSource : EventSource {
    public static readonly EventuousEventSource Log = new();

    [NonEvent]
    public void CommandHandlerNotFound<T>() {
        if (IsEnabled(EventLevel.Error, EventKeywords.All))
            CommandHandlerNotFound(typeof(T).Name);
    }

    [NonEvent]
    public void ErrorHandlingCommand<T>(Exception e) {
        if (IsEnabled(EventLevel.Error, EventKeywords.All))
            ErrorHandlingCommand(typeof(T).Name, e.ToString());
    }

    [NonEvent]
    public void CommandHandlerAlreadyRegistered<T>() {
        if (IsEnabled(EventLevel.Critical, EventKeywords.All))
            CommandHandlerAlreadyRegistered(typeof(T).Name);
    }

    [Event(1, Message = "Handler not found for command: '{0}'", Level = EventLevel.Error)]
    public void CommandHandlerNotFound(string commandType) => WriteEvent(1, commandType);

    [Event(2, Message = "Error handling command: '{0}' {1}", Level = EventLevel.Error)]
    public void ErrorHandlingCommand(string commandType, string exception) => WriteEvent(2, commandType, exception);

    [Event(3, Message = "Command handler already registered for {0}", Level = EventLevel.Critical)]
    public void CommandHandlerAlreadyRegistered(string type) => WriteEvent(3, type);

    // public void UnableToAppendEvents(string stream, string exception) {
    //     
    // }
}