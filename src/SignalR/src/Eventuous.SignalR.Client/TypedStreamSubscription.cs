// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Eventuous.Diagnostics;
using Microsoft.AspNetCore.SignalR.Client;
using static Eventuous.SignalR.SignalRSubscriptionMethods;

namespace Eventuous.SignalR.Client;

/// <summary>
/// A subscription that deserializes stream events into typed objects and dispatches them to registered handlers.
/// Configure handlers with <see cref="On{T}"/> then call <see cref="StartAsync"/> to begin consuming.
/// </summary>
public class TypedStreamSubscription : IAsyncDisposable {
    readonly SignalRSubscriptionClient                               _client;
    readonly string                                                  _stream;
    readonly ulong?                                                  _fromPosition;
    readonly Dictionary<string, Func<object, StreamMeta, ValueTask>> _handlers = new();
    Action<StreamSubscriptionError>?                                 _errorHandler;
    CancellationTokenSource?                                         _cts;
    Task?                                                            _consumeTask;
    bool                                                             _started;

    internal TypedStreamSubscription(SignalRSubscriptionClient client, string stream, ulong? fromPosition) {
        _client       = client;
        _stream       = stream;
        _fromPosition = fromPosition;
    }

    /// <summary>
    /// Registers a handler for events of type <typeparamref name="T"/>. The event type must be registered in <see cref="TypeMap"/>.
    /// Must be called before <see cref="StartAsync"/>.
    /// </summary>
    /// <typeparam name="T">The event type to handle.</typeparam>
    /// <param name="handler">Async callback receiving the deserialized event and stream metadata.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public TypedStreamSubscription On<T>(Func<T, StreamMeta, ValueTask> handler) where T : class {
        if (_started) throw new InvalidOperationException("Cannot register handlers after StartAsync has been called.");

        var eventType = TypeMap.Instance.GetTypeName<T>();
        _handlers[eventType] = (obj, meta) => handler((T)obj, meta);

        return this;
    }

    /// <summary>
    /// Registers an error handler invoked when the subscription encounters a failure.
    /// </summary>
    /// <param name="handler">Callback receiving the error details.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public TypedStreamSubscription OnError(Action<StreamSubscriptionError> handler) {
        _errorHandler = handler;

        return this;
    }

    /// <summary>
    /// Starts consuming events from the stream. Handlers must be registered before calling this method.
    /// Can only be called once.
    /// </summary>
    /// <param name="ct">Cancellation token that stops the subscription when cancelled.</param>
    public async Task StartAsync(CancellationToken ct = default) {
        if (_started) throw new InvalidOperationException("StartAsync has already been called.");

        _started = true;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var channel = Channel.CreateBounded<StreamEventEnvelope>(
            new BoundedChannelOptions(_client.ChannelCapacity) { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait }
        );
        _client.RegisterSubscription(_stream, new(channel, _fromPosition));

        await _client.Connection.InvokeAsync(Subscribe, _stream, _fromPosition, _cts.Token).NoContext();

        _consumeTask = ConsumeLoop(channel.Reader, _cts.Token);
    }

    async Task ConsumeLoop(ChannelReader<StreamEventEnvelope> reader, CancellationToken ct) {
        var serializer    = _client.GetSerializer();
        var enableTracing = _client.TracingEnabled;

        try {
            await foreach (var envelope in reader.ReadAllAsync(ct).NoContext(ct)) {
                if (!_handlers.TryGetValue(envelope.EventType, out var handler)) continue;

                var payload = Encoding.UTF8.GetBytes(envelope.JsonPayload);
                var result  = serializer.DeserializeEvent(payload, envelope.EventType, "application/json");

                if (result is not DeserializationResult.SuccessfullyDeserialized deserialized) continue;

                var meta = new StreamMeta(envelope.Stream, envelope.StreamPosition, envelope.Timestamp);

                Activity? activity = null;

                try {
                    if (enableTracing && envelope.JsonMetadata != null) {
                        activity = StartTraceActivity(envelope.JsonMetadata);
                    }

                    await handler(deserialized.Payload, meta).NoContext();
                } finally {
                    activity?.Dispose();
                }
            }
        } catch (OperationCanceledException) {
            // Expected on dispose/cancellation
        } catch (ChannelClosedException ex) {
            // Channel completed with an error (server subscription failure or connection closed)
            _errorHandler?.Invoke(new() { Stream = _stream, Message = ex.InnerException?.Message ?? "Subscription channel closed" });
        } catch (Exception ex) {
            _errorHandler?.Invoke(new() { Stream = _stream, Message = ex.ToString() });
        }
    }

    static Activity? StartTraceActivity(string jsonMetadata) {
        try {
            var metaDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(jsonMetadata);

            if (metaDict == null) return null;

            var metadata      = new Metadata(metaDict);
            var tracingMeta   = metadata.GetTracingMeta();
            var parentContext = tracingMeta.ToActivityContext(isRemote: true);

            if (parentContext == null) return null;

            return EventuousDiagnostics.ActivitySource.StartActivity(
                "signalr.consume",
                ActivityKind.Consumer,
                parentContext.Value
            );
        } catch (Exception) {
            // Tracing is the best effort; malformed metadata must not break event consumption
            return null;
        }
    }

    public async ValueTask DisposeAsync() {
        if (_cts != null) {
            await _cts.CancelAsync().NoContext();

            if (_consumeTask != null) {
                try { await _consumeTask.NoContext(); } catch (OperationCanceledException) {
                    /* expected */
                }
            }

            _cts.Dispose();
        }

        if (_started) {
            _client.RemoveSubscription(_stream);

            try {
                await _client.Connection.InvokeAsync(Unsubscribe, _stream).NoContext();
            } catch (OperationCanceledException) {
                // Expected during shutdown
            } catch (ObjectDisposedException) {
                // Connection already torn down
            }
        }
    }
}
