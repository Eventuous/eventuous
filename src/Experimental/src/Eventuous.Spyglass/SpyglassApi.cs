// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Eventuous.Spyglass;

public static class SpyglassApi {
    [PublicAPI]
    public static IEndpointRouteBuilder MapEventuousSpyglass(this IEndpointRouteBuilder endpoints) {
        endpoints
            .MapGet(
                "/spyglass/aggregates",
                ([FromServices] InsidePeek peek) => peek.Aggregates
            )
            .ExcludeFromDescription();

        endpoints
            .MapGet(
                "/spyglass/events",
                ([FromServices] TypeMapper? typeMapper) => {
                    var typeMap = typeMapper ?? TypeMap.Instance;
                    var map     = typeMap.GetPrivateMember<Dictionary<string, Type>>("_reverseMap");
                    return map?.Select(x => x.Key);
                }
            )
            .ExcludeFromDescription();

        endpoints
            .MapGet(
                "/spyglass/load/{streamName}",
                ([FromServices] InsidePeek peek, string streamName, [FromQuery] int version)
                    => peek.Load(streamName, version)
            )
            .ExcludeFromDescription();

        return endpoints;
    }
}
