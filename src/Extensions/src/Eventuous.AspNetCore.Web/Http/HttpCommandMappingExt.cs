// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.AspNetCore.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

// ReSharper disable once CheckNamespace
namespace Microsoft.AspNetCore.Routing;

public delegate TCommand ConvertAndEnrichCommand<in TContract, out TCommand>(TContract command, HttpContext httpContext);

public static partial class RouteBuilderExtensions {
    /// <summary>
    /// Map command to HTTP POST endpoint.
    /// The HTTP command type should be annotated with <seealso cref="HttpCommandAttribute"/> attribute.
    /// </summary>
    /// <param name="builder">Endpoint route builder instance</param>
    /// <param name="convert">Function to convert HTTP command to domain command</param>
    /// <typeparam name="TContract">HTTP command type</typeparam>
    /// <typeparam name="TCommand">Domain command type</typeparam>
    /// <typeparam name="TAggregate">Aggregate type</typeparam>
    /// <returns></returns>
    [PublicAPI]
    public static RouteHandlerBuilder MapCommand<TContract, TCommand, TAggregate>(
        this IEndpointRouteBuilder                   builder,
        ConvertAndEnrichCommand<TContract, TCommand> convert
    ) where TAggregate : Aggregate where TCommand : class where TContract : class {
        var attr = typeof(TContract).GetAttribute<HttpCommandAttribute>();
        return Map<TAggregate, TContract, TCommand>(builder, attr?.Route, convert, attr?.PolicyName);
    }

    /// <summary>
    /// Map command to HTTP POST endpoint
    /// </summary>
    /// <param name="builder">Endpoint route builder instance</param>
    /// <param name="route">API route for the POST endpoint</param>
    /// <param name="convert">Function to convert HTTP command to domain command</param>
    /// <param name="policyName">Optional authorization policy name</param>
    /// <typeparam name="TContract">HTTP command type</typeparam>
    /// <typeparam name="TCommand">Domain command type</typeparam>
    /// <typeparam name="TAggregate">Aggregate type</typeparam>
    /// <returns></returns>
    [PublicAPI]
    public static RouteHandlerBuilder MapCommand<TContract, TCommand, TAggregate>(
        this IEndpointRouteBuilder                   builder,
        string?                                      route,
        ConvertAndEnrichCommand<TContract, TCommand> convert,
        string?                                      policyName = null
    ) where TAggregate : Aggregate where TCommand : class where TContract : class
        => Map<TAggregate, TContract, TCommand>(builder, route, convert, policyName);
}
