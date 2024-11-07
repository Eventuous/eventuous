// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions.Consumers;

using Context;

// ReSharper disable once ParameterTypeCanBeEnumerable.Local
public class DefaultConsumer(IEventHandler[] eventHandlers) : IMessageConsumer {
    public async ValueTask Consume(IMessageConsumeContext context) {
        var scope = new Dictionary<string, object> {
            {"SubscriptionId", context.SubscriptionId},
            {"Stream", context.Stream},
            {"MessageType", context.MessageType},
        };

        using var _ = context.LogContext.Logger.BeginScope(scope);

        try {
            if (context.Message == null) {
                context.Ignore<DefaultConsumer>();

                return;
            }

            var typedContext = context.ConvertToGeneric();

            // ReSharper disable once ForCanBeConvertedToForeach
            for (var index = 0; index < eventHandlers.Length; index++) {
                var handler = eventHandlers[index];
                await Handle(typedContext, handler).NoContext();
            }
        } catch (Exception e) {
            context.Nack<DefaultConsumer>(e);
        }

        return;

        async ValueTask Handle(IMessageConsumeContext typedContext, IEventHandler handler) {
            try {
                var status = await handler.HandleEvent(typedContext).NoContext();

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
            } catch (Exception e) {
                context.Nack(handler.DiagnosticName, e);
            }
        }
    }
}
