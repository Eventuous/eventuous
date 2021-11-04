using System.Diagnostics;

namespace Eventuous.Subscriptions.Context;

public interface IMessageConsumeContext {
    string           MessageId     { get; }
    string           MessageType   { get; }
    string           ContentType   { get; }
    string           Stream        { get; }
    DateTime         Created       { get; }
    object?          Message       { get; }
    Metadata?        Metadata      { get; }
    ContextItems     Items         { get; }
    ActivityContext? ParentContext { get; set; }
}

public interface IMessageConsumeContext<out T> where T : class {
    string       EventId     { get; }
    string       EventType   { get; }
    string       ContentType { get; }
    string       Stream      { get; }
    DateTime     Created     { get; }
    T?           Message     { get; }
    Metadata?    Metadata    { get; }
    ContextItems Items       { get; }
}