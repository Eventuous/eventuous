using System.Diagnostics.Tracing;
using System.Xml;

// ReSharper disable MemberCanBePrivate.Global

namespace Eventuous.Diagnostics;

public static class DiagnosticName {
    public const string BaseName = "eventuous";
}

[EventSource(Name = DiagnosticName.BaseName)]
public class EventuousEventSource : EventSource {
    public static readonly EventuousEventSource Log = new();

    const int CommandHandlerNotFoundId          = 1;
    const int ErrorHandlingCommandId            = 2;
    const int CommandHandlerAlreadyRegisteredId = 3;
    const int UnableToStoreAggregateId          = 4;
    const int UnableToReadAggregateId           = 5;
    const int UnableToAppendEventsId            = 6;

    [NonEvent]
    public void CommandHandlerNotFound<T>() => CommandHandlerNotFound(typeof(T).Name);

    [NonEvent]
    public void ErrorHandlingCommand<T>(Exception e) => ErrorHandlingCommand(typeof(T).Name, e.ToString());

    [NonEvent]
    public void CommandHandlerAlreadyRegistered<T>() => CommandHandlerAlreadyRegistered(typeof(T).Name);

    [NonEvent]
    public void UnableToAppendEvents(StreamName stream, Exception exception)
        => UnableToAppendEvents(stream, exception.ToString());

    [NonEvent]
    public void UnableToStoreAggregate<T>(T aggregate, Exception exception) where T : Aggregate {
        if (IsEnabled(EventLevel.Warning, EventKeywords.All))
            UnableToStoreAggregate(typeof(T).Name, aggregate.GetId(), exception.ToString());
    }

    [NonEvent]
    public void UnableToLoadAggregate<T>(string id, Exception exception) where T : Aggregate {
        if (IsEnabled(EventLevel.Warning, EventKeywords.All))
            UnableToLoadAggregate(typeof(T).Name, id, exception.ToString());
    }

    [Event(CommandHandlerNotFoundId, Message = "Handler not found for command: '{0}'", Level = EventLevel.Error)]
    public void CommandHandlerNotFound(string commandType) => WriteEvent(CommandHandlerNotFoundId, commandType);

    [Event(ErrorHandlingCommandId, Message = "Error handling command: '{0}' {1}", Level = EventLevel.Error)]
    public void ErrorHandlingCommand(string commandType, string exception)
        => WriteEvent(ErrorHandlingCommandId, commandType, exception);

    [Event(
        CommandHandlerAlreadyRegisteredId,
        Message = "Command handler already registered for {0}",
        Level = EventLevel.Critical
    )]
    public void CommandHandlerAlreadyRegistered(string type) => WriteEvent(CommandHandlerAlreadyRegisteredId, type);

    [Event(UnableToAppendEventsId, Message = "Unable to append events to {0}: {1}", Level = EventLevel.Error)]
    public void UnableToAppendEvents(string stream, string exception)
        => WriteEvent(UnableToAppendEventsId, stream, exception);

    [Event(
        UnableToStoreAggregateId,
        Message = "Unable to store aggregate {0} with id {1}: {2}",
        Level = EventLevel.Warning
    )]
    public void UnableToStoreAggregate(string type, string id, string exception)
        => WriteEvent(UnableToStoreAggregateId, type, id, exception);

    [Event(
        UnableToReadAggregateId,
        Message = "Unable to read aggregate {0} with id {1}: {2}",
        Level = EventLevel.Warning
    )]
    public void UnableToLoadAggregate(string type, string id, string exception)
        => WriteEvent(UnableToReadAggregateId, type, id, exception);

}