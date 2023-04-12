// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Context;

namespace Eventuous.Gateway;

public interface IGatewayTransform {
    ValueTask<GatewayMessage[]> RouteAndTransform(IMessageConsumeContext context);
}

public interface IGatewayTransform<TProduceOptions> {
    ValueTask<GatewayMessage<TProduceOptions>[]> RouteAndTransform(IMessageConsumeContext context);
}
