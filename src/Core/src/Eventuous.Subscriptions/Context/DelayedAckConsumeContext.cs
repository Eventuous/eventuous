namespace Eventuous.Subscriptions.Context;

/// <summary>
/// Function to acknowledge the message to the transport
/// </summary>
public delegate ValueTask Acknowledge(IMessageConsumeContext ctx);

/// <summary>
/// Function to report a processing failure to the transport
/// </summary>
public delegate ValueTask Fail(IMessageConsumeContext ctx, Exception exception);

/// <summary>
/// Context type that allows to decouple subscriptions from the actual message processing
/// </summary>
public class DelayedAckConsumeContext : WrappedConsumeContext {
    readonly Acknowledge _acknowledge;
    readonly Fail        _fail;

    /// <summary>
    /// Creates a new delayed ACK context instance
    /// </summary>
    /// <param name="inner">The original message context</param>
    /// <param name="acknowledge">Function to ACK the message</param>
    /// <param name="fail">Function to NACK the message in case of failure</param>
    public DelayedAckConsumeContext(IMessageConsumeContext inner, Acknowledge acknowledge, Fail fail) : base(inner) {
        _acknowledge = acknowledge;
        _fail        = fail;
    }

    /// <summary>
    /// Acknowledges that the message has been processed successfully. It also gets called if the message was ignored.
    /// </summary>
    /// <returns></returns>
    public ValueTask Acknowledge() => _acknowledge(this);

    /// <summary>
    /// Reports a message processing failure (NACK)
    /// </summary>
    /// <param name="exception">Exception that occurred during message processing</param>
    /// <returns></returns>
    public ValueTask Fail(Exception exception) => _fail(this, exception);
}