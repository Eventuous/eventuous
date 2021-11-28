using System.Diagnostics;
using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;
using ActivityStatus = Eventuous.Diagnostics.ActivityStatus;
using Exception = System.Exception;

namespace Eventuous.Subscriptions.Consumers;

public class TracedConsumer : MessageConsumer {
    public TracedConsumer(MessageConsumer messageConsumer) {
        _inner = messageConsumer;

        _defaultTags = new[] {
            new KeyValuePair<string, object?>(
                TelemetryTags.Eventuous.Consumer,
                messageConsumer.GetType().Name
            )
        };
    }

    readonly KeyValuePair<string, object?>[] _defaultTags;
    readonly MessageConsumer                _inner;

    public override async ValueTask Consume(IMessageConsumeContext context) {
        if (context.Message == null) return;

        using var activity = Activity.Current?.Context != context.ParentContext
            ? SubscriptionActivity.Start(context, _defaultTags) : Activity.Current;

        activity?.SetContextTags(context)?.Start();

        try {
            await _inner.Consume(context).NoContext();
        }
        catch (Exception e) {
            activity?.SetStatus(ActivityStatus.Error(e, $"Error handling {context.MessageType}"));
            throw;
        }
    }
}