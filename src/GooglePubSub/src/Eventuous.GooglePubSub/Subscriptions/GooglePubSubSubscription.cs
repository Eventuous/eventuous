// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Subscriptions.Logging;
using Google.Api.Gax;
using Google.Protobuf.Collections;
using static Google.Cloud.PubSub.V1.SubscriberClient;

namespace Eventuous.GooglePubSub.Subscriptions;

using Shared;

/// <summary>
/// Google PubSub subscription service
/// </summary>
[PublicAPI]
public class GooglePubSubSubscription : EventSubscription<PubSubSubscriptionOptions> {
    public delegate ValueTask<Reply> HandleEventProcessingFailure(SubscriberClient client, PubsubMessage pubsubMessage, Exception exception);

    readonly HandleEventProcessingFailure _failureHandler;
    readonly SubscriptionName             _subscriptionName;
    readonly TopicName                    _topicName;

    /// <summary>
    /// Creates a Google PubSub subscription service
    /// </summary>
    /// <param name="projectId">GCP project ID</param>
    /// <param name="topicId">Topic where the subscription receives messages from</param>
    /// <param name="subscriptionId">Google PubSub subscription ID (within the project), which must already exist</param>
    /// <param name="consumePipe">Consumer pipeline</param>
    /// <param name="loggerFactory">Logger factory instance</param>
    /// <param name="eventSerializer">Event serializer instance</param>
    /// <param name="configureClient">Optional client configuration callback</param>
    public GooglePubSubSubscription(
            string                           projectId,
            string                           topicId,
            string                           subscriptionId,
            ConsumePipe                      consumePipe,
            ILoggerFactory?                  loggerFactory   = null,
            IEventSerializer?                eventSerializer = null,
            Action<SubscriberClientBuilder>? configureClient = null
        )
        : this(
            new() {
                SubscriptionId         = subscriptionId,
                ProjectId              = projectId,
                TopicId                = topicId,
                ConfigureClientBuilder = configureClient
            },
            consumePipe,
            loggerFactory,
            eventSerializer
        ) { }

    /// <summary>
    /// Creates a Google PubSub subscription service
    /// </summary>
    /// <param name="options">Subscription options <see cref="PubSubSubscriptionOptions"/></param>
    /// <param name="consumePipe">Consumer pipeline</param>
    /// <param name="loggerFactory">Logger factory instance</param>
    /// <param name="eventSerializer">Event serializer</param>
    public GooglePubSubSubscription(
            PubSubSubscriptionOptions options,
            ConsumePipe               consumePipe,
            ILoggerFactory?           loggerFactory,
            IEventSerializer?         eventSerializer
        )
        : base(options, consumePipe, loggerFactory, eventSerializer) {
        _failureHandler   = Ensure.NotNull(options).FailureHandler ?? DefaultEventProcessingErrorHandler;
        _subscriptionName = SubscriptionName.FromProjectSubscription(Ensure.NotEmptyString(options.ProjectId), Ensure.NotEmptyString(options.SubscriptionId));
        _topicName        = TopicName.FromProjectTopic(options.ProjectId, Ensure.NotEmptyString(options.TopicId));

        if (options is { FailureHandler: not null, ThrowOnError: false }) Log.ThrowOnErrorIncompatible();
    }

    protected override async ValueTask Connect(SubscriptionRun run) {
        var builder = new SubscriberClientBuilder { Logger = Log.Logger };
        Options.ConfigureClientBuilder?.Invoke(builder);
        builder.SubscriptionName = _subscriptionName;

        if (Options.CreateSubscription) {
            await CreateSubscription(_subscriptionName, _topicName, builder.EmulatorDetection, Options.ConfigureSubscription, run.Token).NoContext();
        }

        var client = await builder.BuildAsync(run.Token).NoContext();

        Task pumping;

        try {
            // Started inline, not on its own task: StartAsync must have run before teardown can call StopAsync,
            // which otherwise throws on a client that never started.
            pumping = client.StartAsync(Handle);
        } catch {
            // Nothing is registered to release the client yet, and its StopAsync would throw, so dispose it here.
            await client.DisposeAsync().NoContext();

            throw;
        }

        // Nothing else observes this task, so wire its end to Fail explicitly: any end other than a clean
        // StopAsync-driven stop is a drop.
        var reporting = pumping.ContinueWith(
            t => {
                if (t.IsCompletedSuccessfully && run.Token.IsCancellationRequested) return;

                run.Fail(
                    DropReason.ServerError,
                    t.Exception?.GetBaseException() ?? new InvalidOperationException("Google Pub/Sub client task ended before it was stopped")
                );
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
        );

        // StopAsync first, then join: the client's task only ends once StopAsync has run, so joining first
        // would deadlock until the graceful budget expires. Both the join and the disposal are in a finally
        // because StopAsync throws when the graceful budget forces a hard stop — the pump still ends, and
        // skipping the join would let the replacement client start alongside it. Disposed rather than left to
        // the stop path, since this run built the client and releases it whatever the stop did.
        run.OnDisconnect(async ct => {
            try {
                await client.StopAsync(ct).NoContext();
            } finally {
                await reporting.NoContext();
                await client.DisposeAsync().NoContext();
            }
        });

        return;

        async Task<Reply> Handle(PubsubMessage msg, CancellationToken ct) {
            var eventType   = msg.Attributes[Options.Attributes.EventType];
            var contentType = msg.Attributes[Options.Attributes.ContentType];

            Logger.Current = Log;

            var evt = DeserializeData(contentType, eventType, msg.Data.ToByteArray(), _topicName.TopicId);

            var ctx = new MessageConsumeContext(
                msg.MessageId,
                eventType,
                contentType,
                _topicName.TopicId,
                0,
                0,
                0,
                run.NextSequence(),
                msg.PublishTime.ToDateTime(),
                evt,
                AsMeta(msg.Attributes),
                SubscriptionId,
                ct
            );

            try {
                await Handler(ctx).NoContext();

                return Reply.Ack;
            } catch (Exception ex) { return await _failureHandler(client, msg, ex).NoContext(); }
        }

        Metadata AsMeta(MapField<string, string> attributes) => new(attributes.ToDictionary(x => x.Key, object (x) => x.Value)!);
    }

    public async Task CreateSubscription(
            SubscriptionName      subscriptionName,
            TopicName             topicName,
            EmulatorDetection     emulatorDetection,
            Action<Subscription>? configureSubscription,
            CancellationToken     cancellationToken
        ) {
        Logger.Current = Log;
        await PubSub.CreateTopic(topicName, emulatorDetection, (msg, s) => Log.InfoLog?.Log(msg, s), cancellationToken).NoContext();
        await PubSub.CreateSubscription(subscriptionName, topicName, configureSubscription, emulatorDetection, cancellationToken).NoContext();
    }

    static ValueTask<Reply> DefaultEventProcessingErrorHandler(SubscriberClient client, PubsubMessage message, Exception exception) => new(Reply.Nack);
}
