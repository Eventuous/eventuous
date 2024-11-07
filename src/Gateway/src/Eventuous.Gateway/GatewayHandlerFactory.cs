// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Gateway;

[PublicAPI]
public static class GatewayHandlerFactory {
    public static IEventHandler Create<T>(IProducer<T> producer, RouteAndTransform<T> routeAndTransform, bool awaitProduce) where T : class
        => new GatewayHandler<T>(new GatewayProducer<T>(producer), routeAndTransform, awaitProduce);
}
