// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Eventuous.GooglePubSub.CloudRun;

public class CloudRunPubSubSubscription(CloudRunPubSubSubscriptionOptions options, ConsumePipe consumePipe, ILoggerFactory? loggerFactory, IEventSerializer? eventSerializer = null)
    : EventSubscription<CloudRunPubSubSubscriptionOptions>(options, consumePipe, loggerFactory, eventSerializer) {
    protected override ValueTask Subscribe(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    protected override ValueTask Unsubscribe(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    const string DefaultContentType = "application/json";

    /// <summary>
    /// Maps the subscription endpoint to the specified <see cref="WebApplication"/>.
    /// The PubSub trigger for CLoud Run will make POST calls to the endpoint with the message payload.
    /// </summary>
    /// <param name="app"></param>
    /// <param name="path">Optional endpoint path. Default is root path. It must match with the endpoint configuration of the push trigger/</param>
    [PublicAPI]
    public static void MapSubscription(WebApplication app, string path = "/") {
        var subscription = app.Services.GetRequiredService<CloudRunPubSubSubscription>();

        app.MapPost(
            "/",
            async (Envelope envelope, CancellationToken cancellationToken) => {
                if (envelope.Message?.Data == null) {
                    subscription.Log.ErrorLog?.Log("Bad Request: Invalid Pub/Sub message format.");

                    return Results.BadRequest();
                }

                subscription.Log.InfoLog?.Log("Received {@Message}", envelope.Message);
                var data = Convert.FromBase64String(envelope.Message.Data);

                // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                if (envelope.Message.Attributes == null) {
                    subscription.Log.WarnLog?.Log("Message {MessageId} has no attributes", envelope.Message.MessageId);

                    return Results.NoContent();
                }

                var eventType = envelope.Message.Attributes[subscription.Options.Attributes.EventType];

                if (string.IsNullOrWhiteSpace(eventType)) {
                    subscription.Log.WarnLog?.Log("Message {MessageId} has no event type", envelope.Message.MessageId);

                    return Results.NoContent();
                }

                var contentType = envelope.Message.Attributes.GetValueOrDefault(subscription.Options.Attributes.ContentType, DefaultContentType);
                var message     = subscription.DeserializeData(contentType, eventType, data, subscription.Options.TopicId);

                var messageId = envelope.Message.Attributes.TryGetValue(subscription.Options.Attributes.MessageId, out var id)
                    ? id
                    : envelope.Message.MessageId;

                var context = new MessageConsumeContext(
                    messageId,
                    eventType,
                    contentType,
                    subscription.Options.TopicId,
                    0,
                    0,
                    0,
                    subscription.Sequence++,
                    envelope.Message.PublishTime,
                    message,
                    null,
                    subscription.SubscriptionId,
                    cancellationToken
                ) { LogContext = subscription.Log };

                await subscription.Handler(context);

                return Results.NoContent();
            }
        );
    }

    [UsedImplicitly]
    record Message(string MessageId, Dictionary<string, string> Attributes, string Data, DateTime PublishTime);

    [UsedImplicitly]
    record Envelope(Message? Message);
}
