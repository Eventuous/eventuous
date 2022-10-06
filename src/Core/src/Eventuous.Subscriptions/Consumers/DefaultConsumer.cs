// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Tools;

namespace Eventuous.Subscriptions.Consumers;

public class DefaultConsumer : IMessageConsumer {
    readonly IEventHandler[] _eventHandlers;

    public DefaultConsumer(IEventHandler[] eventHandlers) => _eventHandlers = eventHandlers;

    public async ValueTask Consume(IMessageConsumeContext context) {
        try {
            if (context.Message == null) {
                context.Ignore<DefaultConsumer>();
                return;
            }

            var tasks = _eventHandlers.Select(handler => Handle(handler));
            await tasks.WhenAll().NoContext();
        }
        catch (Exception e) {
            context.Nack<DefaultConsumer>(e);
        }

        async ValueTask Handle(IEventHandler handler) {
            try {
                var status = await handler.HandleEvent(context).NoContext();

                switch (status) {
                    case EventHandlingStatus.Success:
                        context.Ack(handler.DiagnosticName);
                        break;
                    case EventHandlingStatus.Ignored:
                        context.Ignore(handler.DiagnosticName);
                        break;
                    case EventHandlingStatus.Failure:
                        context.Nack(handler.DiagnosticName, null);
                        break;
                }
            }
            catch (Exception e) {
                context.Nack(handler.DiagnosticName, e);
            }
        }
    }
}