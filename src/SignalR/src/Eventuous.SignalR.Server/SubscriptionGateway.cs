// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using Eventuous.Subscriptions.Filters;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Eventuous.SignalR.Server;

/// <summary>
/// Manages per-connection, per-stream event store subscriptions and routes events to SignalR clients via a <see cref="SignalRProducer{THub}"/>.
/// </summary>
/// <typeparam name="THub">The SignalR hub type.</typeparam>
public class SubscriptionGateway<THub>(
        IHubContext<THub>     hubContext,
        SignalRProducer<THub> producer,
        SignalRGatewayOptions options,
        ILoggerFactory        loggerFactory,
        IEventSerializer?     eventSerializer = null
    )
    : IAsyncDisposable
    where THub : Hub {
    readonly SubscriptionFactory                                                           _subscriptionFactory = options.SubscriptionFactory;
    readonly IEventSerializer                                                              _eventSerializer     = eventSerializer ?? EventSerializer.Default;
    readonly ILogger                                                                       _logger              = loggerFactory.CreateLogger<SubscriptionGateway<THub>>();
    readonly ConcurrentDictionary<(string ConnectionId, string Stream), SubscriptionState> _subscriptions       = new();

    /// <summary>
    /// Starts an event store subscription for the specified connection and stream. Replaces any existing subscription for the same key.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID.</param>
    /// <param name="stream">The stream name to subscribe to.</param>
    /// <param name="fromPosition">Optional starting position (exclusive).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task SubscribeAsync(string connectionId, string stream, ulong? fromPosition, CancellationToken ct = default) {
        var key = (connectionId, stream);

        // Remove an existing subscription for the same key
        if (_subscriptions.TryRemove(key, out var existing)) {
            await StopSubscription(existing).NoContext();
        }

        var transform = SignalRTransform.Create(connectionId, stream, _eventSerializer);
        var handler   = GatewayHandlerFactory.Create(producer, transform, awaitProduce: true);
        var pipe      = new ConsumePipe();
        pipe.AddDefaultConsumer(handler);

        var subscriptionId = $"signalr-{connectionId}-{stream}";
        var subscription   = _subscriptionFactory(new(stream), fromPosition, pipe, subscriptionId);

        var cts   = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var state = new SubscriptionState(subscription, pipe, cts);

        _subscriptions[key] = state;

        // Start subscription in background
        _ = Task.Run(
            async () => {
                try {
                    await subscription.Subscribe(
                            _ => _logger.LogDebug("Subscribed {SubscriptionId}", subscriptionId),
                            (id, reason, ex) => _logger.LogWarning(ex, "Subscription {SubscriptionId} dropped: {Reason}", id, reason),
                            cts.Token
                        )
                        .NoContext();
                } catch (OperationCanceledException) {
                    // Expected on unsubscribing
                } catch (Exception ex) {
                    _logger.LogError(ex, "Subscription {SubscriptionId} failed", subscriptionId);
                    _subscriptions.TryRemove(key, out _);

                    // Notify the client about the failure
                    try {
                        var client = hubContext.Clients.Client(connectionId);

                        await client.SendAsync(
                                SignalRSubscriptionMethods.StreamError,
                                new StreamSubscriptionError { Stream = stream, Message = ex.Message },
                                CancellationToken.None
                            )
                            .NoContext();
                    } catch (Exception notifyEx) {
                        _logger.LogDebug(notifyEx, "Failed to notify client {ConnectionId} about subscription error", connectionId);
                    }
                }
            },
            cts.Token
        );
    }

    /// <summary>
    /// Stops the subscription for the specified connection and stream.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID.</param>
    /// <param name="stream">The stream name to unsubscribe from.</param>
    public async Task UnsubscribeAsync(string connectionId, string stream) {
        if (_subscriptions.TryRemove((connectionId, stream), out var state)) {
            await StopSubscription(state).NoContext();
        }
    }

    /// <summary>
    /// Stops and removes all subscriptions for the specified connection (e.g., on disconnect).
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID to clean up.</param>
    public async Task RemoveConnectionAsync(string connectionId) {
        foreach (var key in _subscriptions.Keys.Where(k => k.ConnectionId == connectionId).ToList()) {
            if (_subscriptions.TryRemove(key, out var state)) {
                await StopSubscription(state).NoContext();
            }
        }
    }

    async Task StopSubscription(SubscriptionState state) {
        await state.Cts.CancelAsync();

        try {
            await state.Subscription.Unsubscribe(_ => { }, CancellationToken.None).NoContext();
        } catch (OperationCanceledException) {
            // Expected during cancellation
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Error during subscription unsubscribe cleanup");
        }

        await state.Pipe.DisposeAsync().NoContext();
        state.Cts.Dispose();
    }

    public async ValueTask DisposeAsync() {
        foreach (var (_, state) in _subscriptions) {
            await StopSubscription(state).NoContext();
        }

        _subscriptions.Clear();
    }

    record SubscriptionState(IMessageSubscription Subscription, ConsumePipe Pipe, CancellationTokenSource Cts);
}
