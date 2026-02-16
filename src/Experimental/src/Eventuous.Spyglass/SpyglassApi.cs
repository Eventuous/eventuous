// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.Spyglass;

public static class SpyglassApi {
    const string EndpointRouteBuilder = "__EndpointRouteBuilder";

    /// <summary>
    /// Adds Spyglass endpoints using the given access key.
    /// </summary>
    /// <param name="builder">Endpoint route builder</param>
    /// <param name="key">Access key that will be provided to the API in headers</param>
    /// <returns></returns>
    [PublicAPI]
    public static IEndpointRouteBuilder MapEventuousSpyglass(this IEndpointRouteBuilder builder, string? key) {
        var logger = builder.ServiceProvider.GetRequiredService<ILogger<IApplicationBuilder>>();

        if (!builder.ServiceProvider.GetRequiredService<IWebHostEnvironment>().IsDevelopment() && key == null) {
            logger.LogWarning("Insecure Spyglass API is only available in development environment");
            key = Guid.NewGuid().ToString("N");
            logger.LogInformation("Using generated key: {Key}", key);
        }

        if (key == null) {
            logger.LogWarning("Spyglass API is not secured, ensure that it's not exposed to the Internet");
        }

        builder.MapGet("/spyglass/ping", (HttpRequest request) => CheckAndReturn(request, () => "v1.0"))
            .ExcludeFromDescription();

        builder.MapGet(
                "/spyglass/aggregates",
                (HttpRequest request) => CheckAndReturn(request, SpyglassRegistry.GetAggregates)
            )
            .ExcludeFromDescription();

        builder.MapGet(
                "/spyglass/events",
                (HttpRequest request, [FromServices] ITypeMapper? typeMapper) => {
                    var typeMap = typeMapper ?? TypeMap.Instance;

                    return typeMap is not ITypeMapperExt typeMapExt
                        ? Results.Problem("Type mapper doesn't support listing registered types", statusCode: 500)
                        : CheckAndReturn(request, () => typeMapExt.GetRegisteredTypes());
                }
            )
            .ExcludeFromDescription();

        builder.MapGet(
                "/spyglass/load/{typeId:guid}/{entityId}",
                async (HttpRequest request, [FromServices] IEventStore eventStore, [FromServices] StreamNameMap? streamNameMap, Guid typeId, string entityId, [FromQuery] int version) => {
                    if (!Authorized(request)) return Results.Unauthorized();

                    var aggInfo = SpyglassRegistry.FindById(typeId);

                    if (aggInfo is null) return Results.NotFound($"No registered type found for id '{typeId}'");

                    var streamName = aggInfo.GetStreamName(streamNameMap, entityId);
                    var result     = await aggInfo.LoadDelegate(eventStore, streamName, version);

                    return result is null
                        ? Results.NotFound($"Stream '{streamName}' not found")
                        : Results.Ok(new { result.State, Events = result.Events.Select(e => new { e.EventType, e.Payload }) });
                }
            )
            .ExcludeFromDescription();

        return builder;

        IResult CheckAndReturn<T>(HttpRequest request, Func<T> getResult)
            => Authorized(request) ? Results.Ok(getResult()) : Results.Unauthorized();

        bool Authorized(HttpRequest request)
            => key == null || (request.Headers.TryGetValue("X-Eventuous", out var k) && k[0] == key);
    }

    /// <summary>
    /// Adds Spyglass endpoints using the given or generated access key.
    /// </summary>
    /// <param name="app">Application builder</param>
    /// <param name="key">Access key that will be provided to the API in headers. If not provided, it will be generated
    /// if the environment is Development, otherwise the call fails</param>
    /// <returns></returns>
    [PublicAPI]
    public static IApplicationBuilder MapEventuousSpyglass(this IApplicationBuilder app, string? key = null) {
        if (!app.Properties.ContainsKey(EndpointRouteBuilder)) {
            app.UseRouting();
        }

        app.UseEndpoints(builder => builder.MapEventuousSpyglass(key));

        return app;
    }
}
