// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using static Eventuous.SignalR.SignalRSubscriptionMethods;

namespace Eventuous.SignalR.Client;

/// <summary>
/// Client that subscribes to event streams over a SignalR connection and yields events as <see cref="StreamEventEnvelope"/> instances.
/// </summary>
public class SignalRSubscriptionClient : IAsyncDisposable {
    readonly SignalRSubscriptionClientOptions                _options;
    readonly ConcurrentDictionary<string, SubscriptionState> _subscriptions = new();
    readonly IDisposable                                     _eventRegistration;
    readonly IDisposable                                     _errorRegistration;
    bool                                                     _disposed;

    /// <summary>
    /// Creates a new <see cref="SignalRSubscriptionClient"/> bound to the specified hub connection.
    /// </summary>
    /// <param name="connection">The SignalR hub connection to use.</param>
    /// <param name="options">Optional client configuration.</param>
    public SignalRSubscriptionClient(HubConnection connection, SignalRSubscriptionClientOptions? options = null) {
        Connection             =  connection;
        _options               =  options ?? new SignalRSubscriptionClientOptions();
        _eventRegistration     =  Connection.On<StreamEventEnvelope>(StreamEvent, OnStreamEvent);
        _errorRegistration     =  Connection.On<StreamSubscriptionError>(StreamError, OnStreamError);
        Connection.Reconnected += OnReconnected;
        Connection.Closed      += OnClosed;
    }

    /// <summary>
    /// Subscribes to a stream and yields events as they arrive. Only one subscription per stream is active at a time;
    /// calling this again for the same stream replaces the previous subscription.
    /// </summary>
    /// <param name="stream">The stream name to subscribe to.</param>
    /// <param name="fromPosition">Optional starting position (exclusive). When <c>null</c>, subscribes from the beginning.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An async enumerable of stream event envelopes.</returns>
    public async IAsyncEnumerable<StreamEventEnvelope> SubscribeAsync(
            string                                     stream,
            ulong?                                     fromPosition,
            [EnumeratorCancellation] CancellationToken ct = default
        ) {
        var channel = Channel.CreateBounded<StreamEventEnvelope>(
            new BoundedChannelOptions(_options.ChannelCapacity) { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait }
        );

        // Complete any existing subscription for this stream before replacing
        if (_subscriptions.TryRemove(stream, out var previous)) {
            previous.Channel.Writer.TryComplete();
        }

        var state = new SubscriptionState(channel, fromPosition);
        _subscriptions[stream] = state;

        try {
            await Connection.InvokeAsync(Subscribe, stream, fromPosition, ct).NoContext();

            await foreach (var envelope in channel.Reader.ReadAllAsync(ct).NoContext(ct)) {
                yield return envelope;
            }
        } finally {
            _subscriptions.TryRemove(stream, out _);
        }
    }

    /// <summary>
    /// Unsubscribes from a previously subscribed stream.
    /// </summary>
    /// <param name="stream">The stream name to unsubscribe from.</param>
    public async Task UnsubscribeAsync(string stream) {
        if (_subscriptions.TryRemove(stream, out var state)) {
            state.Channel.Writer.TryComplete();

            await Connection.InvokeAsync(Unsubscribe, stream).NoContext();
        }
    }

    internal IEventSerializer GetSerializer() => _options.Serializer ?? EventSerializer.Default;

    internal bool TracingEnabled => _options.EnableTracing;

    internal int ChannelCapacity => _options.ChannelCapacity;

    internal SubscriptionState? GetSubscriptionState(string stream)
        => _subscriptions.GetValueOrDefault(stream);

    internal void RegisterSubscription(string stream, SubscriptionState state)
        => _subscriptions[stream] = state;

    internal void RemoveSubscription(string stream) {
        if (_subscriptions.TryRemove(stream, out var state)) {
            state.Channel.Writer.TryComplete();
        }
    }

    internal HubConnection Connection { get; }

    /// <summary>
    /// Creates a <see cref="TypedStreamSubscription"/> that deserializes events into typed objects and dispatches them to registered handlers.
    /// </summary>
    /// <param name="stream">The stream name to subscribe to.</param>
    /// <param name="fromPosition">Optional starting position (exclusive).</param>
    /// <returns>A typed stream subscription that can be configured with event handlers before starting.</returns>
    public TypedStreamSubscription SubscribeTyped(string stream, ulong? fromPosition)
        => new(this, stream, fromPosition);

    void OnStreamEvent(StreamEventEnvelope envelope) {
        if (!_subscriptions.TryGetValue(envelope.Stream, out var state)) return;

        // Deduplication: skip events at or before the last seen position
        if (state.LastPosition.HasValue && envelope.StreamPosition <= state.LastPosition.Value) return;

        state.LastPosition = envelope.StreamPosition;
        state.Channel.Writer.TryWrite(envelope);
    }

    void OnStreamError(StreamSubscriptionError error) {
        if (_subscriptions.TryRemove(error.Stream, out var state)) {
            state.Channel.Writer.TryComplete(new Exception(error.Message));
        }
    }

    async Task OnReconnected(string? connectionId) {
        foreach (var (stream, state) in _subscriptions) {
            try {
                await Connection.InvokeAsync(Subscribe, stream, state.LastPosition, CancellationToken.None).NoContext();
            } catch (OperationCanceledException) {
                // Connection was disposed during reconnect
            } catch (ObjectDisposedException) {
                // Connection already torn down
            }
        }
    }

    Task OnClosed(Exception? exception) {
        foreach (var (_, state) in _subscriptions) {
            state.Channel.Writer.TryComplete(exception);
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) return;

        _disposed = true;

        Connection.Reconnected -= OnReconnected;
        Connection.Closed      -= OnClosed;

        foreach (var (stream, state) in _subscriptions) {
            state.Channel.Writer.TryComplete();

            try {
                await Connection.InvokeAsync(Unsubscribe, stream).NoContext();
            } catch (OperationCanceledException) {
                // Expected during shutdown
            } catch (ObjectDisposedException) {
                // Connection already torn down
            }
        }

        _subscriptions.Clear();
        _eventRegistration.Dispose();
        _errorRegistration.Dispose();
    }

    internal class SubscriptionState(Channel<StreamEventEnvelope> channel, ulong? initialPosition) {
        public Channel<StreamEventEnvelope> Channel      { get; }      = channel;
        public ulong?                       LastPosition { get; set; } = initialPosition;
    }
}
