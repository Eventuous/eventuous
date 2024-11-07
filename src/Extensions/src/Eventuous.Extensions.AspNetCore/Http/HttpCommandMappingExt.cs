// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Extensions.AspNetCore;

// ReSharper disable once CheckNamespace
namespace Microsoft.AspNetCore.Routing;

using Builder;
using Http;

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
    /// <typeparam name="TState">State type</typeparam>
    /// <returns></returns>
    public static RouteHandlerBuilder MapCommand<TContract, TCommand, TState>(
            this IEndpointRouteBuilder                   builder,
            ConvertAndEnrichCommand<TContract, TCommand> convert
        ) where TState : State<TState>, new() where TCommand : class where TContract : class {
        var attr = typeof(TContract).GetAttribute<HttpCommandAttribute>();

        return MapInternal<TState, TContract, TCommand>(builder, attr?.Route, convert, attr?.PolicyName);
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
    /// <typeparam name="TState">State type</typeparam>
    /// <returns></returns>
    public static RouteHandlerBuilder MapCommand<TContract, TCommand, TState>(
            this IEndpointRouteBuilder                   builder,
            string?                                      route,
            ConvertAndEnrichCommand<TContract, TCommand> convert,
            string?                                      policyName = null
        ) where TState : State<TState>, new() where TCommand : class where TContract : class
        => MapInternal<TState, TContract, TCommand>(builder, route, convert, policyName);
}
