// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.Spyglass;

public static class SpyglassApi {
    [PublicAPI]
    public static IEndpointRouteBuilder MapEventuousSpyglass(this WebApplication app, string? key) {
        if (!app.Environment.IsDevelopment() && key == null) {
            app.Logger.LogWarning("Insecure Spyglass API is only available in development environment");
            key = Guid.NewGuid().ToString("N");
            app.Logger.LogInformation("Using generated key: {Key}", key);
        }

        app
            .MapGet(
                "/spyglass/aggregates",
                (HttpRequest request, [FromServices] InsidePeek peek) => CheckAndReturn(request, () => peek.Aggregates)
            )
            .ExcludeFromDescription();

        app
            .MapGet(
                "/spyglass/events",
                (HttpRequest request, [FromServices] TypeMapper? typeMapper) => {
                    var typeMap = typeMapper ?? TypeMap.Instance;
                    return CheckAndReturn(request, () => typeMap.ReverseMap.Select(x => x.Key));
                }
            )
            .ExcludeFromDescription();

        app
            .MapGet(
                "/spyglass/load/{streamName}",
                (HttpRequest request, [FromServices] InsidePeek peek, string streamName, [FromQuery] int version)
                    => CheckAndReturnAsync(request, () => peek.Load(streamName, version))
            )
            .ExcludeFromDescription();

        return app;

        async Task<IResult> CheckAndReturnAsync<T>(HttpRequest request, Func<Task<T>> getResult)
            => Authorized(request) ? Results.Ok(await getResult()) : Results.Unauthorized();

        IResult CheckAndReturn<T>(HttpRequest request, Func<T> getResult)
            => Authorized(request) ? Results.Ok(getResult()) : Results.Unauthorized();

        bool Authorized(HttpRequest request)
            => key == null || (request.Headers.TryGetValue("X-Eventuous", out var k) && k[0] == key);
    }
}
