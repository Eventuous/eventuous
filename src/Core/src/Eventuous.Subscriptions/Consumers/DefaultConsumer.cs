using System.Diagnostics;
using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions.Consumers;

public class DefaultConsumer : IMessageConsumer {
    readonly IEventHandler[] _eventHandlers;
    readonly bool            _throwOnError;
    readonly ILogger?        _log;

    public DefaultConsumer(IEventHandler[] eventHandlers, bool throwOnError, ILogger? log = null) {
        _eventHandlers = eventHandlers;
        _throwOnError  = throwOnError;
        _log           = log;
    }

    public async ValueTask Consume(
        IMessageConsumeContext context,
        CancellationToken      cancellationToken
    ) {
        using var activity = Activity.Current?.Context != context.ParentContext
            ? SubscriptionActivity.Start(context) : Activity.Current;

        try {
            if (context.Message != null) {
                await Task.WhenAll(
                        _eventHandlers.Select(
                            x => x.HandleEvent(
                                context,
                                cancellationToken
                            )
                        )
                    )
                    .NoContext();
            }
        }
        catch (Exception e) {
            activity?.SetStatus(ActivityStatus.Error(e, $"Error handling {context.EventType}"));

            _log?.Log(
                _throwOnError ? LogLevel.Error : LogLevel.Warning,
                e,
                "Error when handling the event {Stream} {Type}",
                context.Stream,
                context.EventType
            );

            if (_throwOnError)
                throw new SubscriptionException(
                    context.Stream,
                    context.EventType,
                    context.Message,
                    e
                );
        }
    }
}