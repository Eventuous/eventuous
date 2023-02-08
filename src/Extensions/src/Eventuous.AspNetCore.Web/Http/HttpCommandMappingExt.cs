// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.AspNetCore.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

// ReSharper disable once CheckNamespace
namespace Microsoft.AspNetCore.Routing;

public delegate TCommand ConvertAndEnrichCommand<in TContract, out TCommand>(TContract command, HttpContext httpContext);

public static partial class RouteBuilderExtensions {
    [PublicAPI]
    public static RouteHandlerBuilder MapCommand<TContract, TCommand, TAggregate>(
        this IEndpointRouteBuilder                   builder,
        ConvertAndEnrichCommand<TContract, TCommand> convert
    ) where TAggregate : Aggregate where TCommand : class where TContract : class {
        var attr  = typeof(TCommand).GetAttribute<HttpCommandAttribute>();
        return Map<TAggregate, TContract, TCommand>(builder, attr?.Route, convert);
    }

    public static RouteHandlerBuilder MapCommand<TContract, TCommand, TAggregate>(
        this IEndpointRouteBuilder                   builder,
        string?                                      route,
        ConvertAndEnrichCommand<TContract, TCommand> convert
    ) where TAggregate : Aggregate where TCommand : class where TContract : class
        => Map<TAggregate, TContract, TCommand>(builder, route, convert);
}
