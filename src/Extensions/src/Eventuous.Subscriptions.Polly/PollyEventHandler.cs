// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using PollyContext = Polly.Context;

namespace Eventuous.Subscriptions.Polly;

using Context;
using Logging;

/// <summary>
/// Wrapping handler to execute the inner handler with a given retry policy
/// </summary>
public class PollyEventHandler : IEventHandler {
    readonly IEventHandler _inner;
    readonly IAsyncPolicy  _retryPolicy;

    public PollyEventHandler(IEventHandler inner, IAsyncPolicy retryPolicy) {
        _inner         = inner;
        _retryPolicy   = retryPolicy;
        DiagnosticName = _inner.DiagnosticName;
    }

    public string DiagnosticName { get; }

    public async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        const string retryKey = "eventuous-retry";

        var pollyContext = new PollyContext { { retryKey, new RetryCounter() } };
        return await _retryPolicy.ExecuteAsync(Execute, pollyContext).NoContext();

        async Task<EventHandlingStatus> Execute(PollyContext ctx) {
            try {
                return await _inner.HandleEvent(context).NoContext();
            }
            catch (Exception e) {
                var counter = ctx[retryKey] as RetryCounter;
                context.LogContext.FailedToHandleMessageWithRetry(DiagnosticName, context.MessageType, counter!.Counter, e);
                counter.Increase();
                throw;
            }
        }
    }

    class RetryCounter {
        public int Counter { get; private set; }

        public void Increase()
            => Counter++;
    }
}
