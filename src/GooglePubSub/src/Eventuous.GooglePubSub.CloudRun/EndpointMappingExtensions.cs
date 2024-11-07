// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.GooglePubSub.CloudRun;

// ReSharper disable CheckNamespace

namespace Microsoft.AspNetCore.Builder;

public static class EndpointMappingExtensions {
    public static WebApplication MapCloudRunPubSubSubscription(this WebApplication app, string path = "/") {
        CloudRunPubSubSubscription.MapSubscription(app, path);

        return app;
    }
}
