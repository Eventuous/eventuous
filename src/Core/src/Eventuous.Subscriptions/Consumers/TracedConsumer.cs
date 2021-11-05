using System.Diagnostics;
using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;
using static Eventuous.Diagnostics.TelemetryTags;
using ActivityStatus = Eventuous.Diagnostics.ActivityStatus;
using Exception = System.Exception;

namespace Eventuous.Subscriptions.Consumers;

public class TracedConsumer : IMessageConsumer {
    public TracedConsumer(IMessageConsumer messageConsumer) {
        _inner = messageConsumer;

        _defaultTags = new[] {
            new KeyValuePair<string, object?>(
                TelemetryTags.Eventuous.Consumer,
                messageConsumer.GetType().Name
            )
        };
    }

    readonly KeyValuePair<string, object?>[] _defaultTags;
    readonly IMessageConsumer                _inner;

    public async ValueTask Consume(
        IMessageConsumeContext context,
        CancellationToken      cancellationToken
    ) {
        if (context.Message == null) return;

        using var activity = Activity.Current?.Context != context.ParentContext
            ? SubscriptionActivity.Start(context, _defaultTags) : Activity.Current;

        activity?.SetContextTags(context)?.Start();

        try {
            await _inner.Consume(context, cancellationToken);
        }
        catch (Exception e) {
            activity?.SetStatus(ActivityStatus.Error(e, $"Error handling {context.MessageType}"));
            throw;
        }
    }
}

public class TracedEventHandler : IEventHandler {
    public TracedEventHandler(IEventHandler eventHandler) {
        _inner = eventHandler;

        _defaultTags = new[] {
            new KeyValuePair<string, object?>(
                TelemetryTags.Eventuous.EventHandler,
                eventHandler.GetType().Name
            )
        };
    }

    readonly IEventHandler                   _inner;
    readonly KeyValuePair<string, object?>[] _defaultTags;

    public async Task HandleEvent(
        IMessageConsumeContext context,
        CancellationToken      cancellationToken
    ) {
        using var activity = Activity.Current?.Context != context.ParentContext
            ? SubscriptionActivity.Start(context, _defaultTags) : Activity.Current;

        activity?.SetContextTags(context)?.Start();

        try {
            await _inner.HandleEvent(context, cancellationToken);
        }
        catch (Exception e) {
            activity?.SetStatus(ActivityStatus.Error(e, $"Error handling {context.MessageType}"));
            throw;
        }
    }
}