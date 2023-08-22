// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.Spyglass;

public static class SpyglassApi {
    [PublicAPI]
    public static IApplicationBuilder MapEventuousSpyglass(this IApplicationBuilder app, string? key = null) {
        var logger = app.ApplicationServices.GetRequiredService<ILogger<IApplicationBuilder>>();

        if (!app.ApplicationServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment() && key == null) {
            logger.LogWarning("Insecure Spyglass API is only available in development environment");
            key = Guid.NewGuid().ToString("N");
            logger.LogInformation("Using generated key: {Key}", key);
        }

        if (key == null) {
            logger.LogWarning("Spyglass API is not secured, ensure that it's not exposed to the Internet");
        }

        app.UseRouting();

        app.UseEndpoints(
            builder => {
                builder.MapGet("/spyglass/ping", (HttpRequest request) => CheckAndReturn(request, () => "Okay"))
                    .ExcludeFromDescription();

                builder.MapGet(
                        "/spyglass/aggregates",
                        (HttpRequest request, [FromServices] InsidePeek peek) => CheckAndReturn(request, () => peek.Aggregates)
                    )
                    .ExcludeFromDescription();

                builder.MapGet(
                        "/spyglass/events",
                        (HttpRequest request, [FromServices] TypeMapper? typeMapper) => {
                            var typeMap = typeMapper ?? TypeMap.Instance;

                            return CheckAndReturn(request, () => typeMap.ReverseMap.Select(x => x.Key));
                        }
                    )
                    .ExcludeFromDescription();

                builder.MapGet(
                        "/spyglass/load/{streamName}",
                        (HttpRequest request, [FromServices] InsidePeek peek, string streamName, [FromQuery] int version)
                            => CheckAndReturnAsync(request, () => peek.Load(streamName, version))
                    )
                    .ExcludeFromDescription();
            }
        );

        return app;

        async Task<IResult> CheckAndReturnAsync<T>(HttpRequest request, Func<Task<T>> getResult)
            => Authorized(request) ? Results.Ok(await getResult()) : Results.Unauthorized();

        IResult CheckAndReturn<T>(HttpRequest request, Func<T> getResult)
            => Authorized(request) ? Results.Ok(getResult()) : Results.Unauthorized();

        bool Authorized(HttpRequest request)
            => key == null || (request.Headers.TryGetValue("X-Eventuous", out var k) && k[0] == key);
    }
}
