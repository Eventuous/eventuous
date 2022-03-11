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
    const int CommandHandledId                  = 3;
    const int CommandHandlerAlreadyRegisteredId = 4;
    const int UnableToStoreAggregateId          = 5;
    const int UnableToReadAggregateId           = 6;
    const int UnableToAppendEventsId            = 7;
    const int TypeNotMappedToNameId             = 8;
    const int TypeNameNotMappedToTypeId         = 9;
    const int TypeMapRegisteredId               = 10;

    [NonEvent]
    public void CommandHandlerNotFound(Type type) => CommandHandlerNotFound(type.Name);

    [NonEvent]
    public void ErrorHandlingCommand(Type type, Exception e) => ErrorHandlingCommand(type.Name, e.ToString());

    [NonEvent]
    public void CommandHandled(Type commandType) {
        if (IsEnabled(EventLevel.Verbose, EventKeywords.All))
            CommandHandled(commandType.Name);
    }
    
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

    [NonEvent]
    public void TypeNotMappedToName(Type type) => TypeNotMappedToName(type.Name);

    [Event(CommandHandlerNotFoundId, Message = "Handler not found for command: '{0}'", Level = EventLevel.Error)]
    public void CommandHandlerNotFound(string commandType) => WriteEvent(CommandHandlerNotFoundId, commandType);

    [Event(ErrorHandlingCommandId, Message = "Error handling command: '{0}' {1}", Level = EventLevel.Error)]
    public void ErrorHandlingCommand(string commandType, string exception)
        => WriteEvent(ErrorHandlingCommandId, commandType, exception);

    [Event(CommandHandledId, Message = "Command handled: '{0}'", Level = EventLevel.Verbose)]
    public void CommandHandled(string commandType) => WriteEvent(CommandHandledId, commandType);
    
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

    [Event(TypeNotMappedToNameId, Message = "Type {0} is not registered in the type map", Level = EventLevel.Error)]
    public void TypeNotMappedToName(string type) => WriteEvent(TypeNotMappedToNameId, type);

    [Event(TypeNameNotMappedToTypeId, Message = "Type name {0} is not mapped to any type", Level = EventLevel.Error)]
    public void TypeNameNotMappedToType(string typeName) => WriteEvent(TypeNameNotMappedToTypeId, typeName);

    [Event(TypeMapRegisteredId, Message = "Type {0} registered as {1}", Level = EventLevel.Verbose)]
    public void TypeMapRegistered(string type, string typeName) {
        if (IsEnabled(EventLevel.Verbose, EventKeywords.All))
            WriteEvent(TypeMapRegisteredId, type, typeName);
    }
}
