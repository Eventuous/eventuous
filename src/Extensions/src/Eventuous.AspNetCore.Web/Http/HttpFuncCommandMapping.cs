// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable CheckNamespace

using Eventuous.AspNetCore.Web;
using Eventuous.AspNetCore.Web.Diagnostics;

namespace Microsoft.AspNetCore.Routing;

using Builder;
using Http;
using static Http.StatusCodes;

public static partial class RouteBuilderExtensions {
    /// <summary>
    /// Map command to HTTP POST endpoint for being executed by a functional service.
    /// The HTTP command type should be annotated with <seealso cref="HttpCommandAttribute"/> attribute.
    /// </summary>
    /// <param name="builder">Endpoint route builder instance</param>
    /// <param name="enrichCommand">A function to populate command props from HttpContext</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <typeparam name="TState">State type on which the command will operate</typeparam>
    /// <returns></returns>
    public static RouteHandlerBuilder MapCommandFunc<TCommand, TState>(
            this IEndpointRouteBuilder              builder,
            EnrichCommandFromHttpContext<TCommand>? enrichCommand = null
        )
        where TState : State<TState>
        where TCommand : class {
        var attr = typeof(TCommand).GetAttribute<HttpCommandAttribute>();

        return builder.MapCommandFunc<TCommand, TState, Result<TState>>(attr?.Route, enrichCommand, attr?.PolicyName);
    }

    /// <summary>
    /// Map command to HTTP POST endpoint for being executed by a functional service.
    /// The HTTP command type should be annotated with <seealso cref="HttpCommandAttribute"/> attribute.
    /// </summary>
    /// <param name="builder">Endpoint route builder instance</param>
    /// <param name="enrichCommand">A function to populate command props from HttpContext</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <typeparam name="TState">State type on which the command will operate</typeparam>
    /// <typeparam name="TResult">Result type that will be returned</typeparam>
    /// <returns></returns>
    public static RouteHandlerBuilder MapCommandFunc<TCommand, TState, TResult>(
            this IEndpointRouteBuilder              builder,
            EnrichCommandFromHttpContext<TCommand>? enrichCommand = null
        )
        where TState : State<TState>, new()
        where TCommand : class
        where TResult : Result<TState> {
        var attr = typeof(TCommand).GetAttribute<HttpCommandAttribute>();

        return builder.MapCommandFunc<TCommand, TState, TResult>(attr?.Route, enrichCommand, attr?.PolicyName);
    }

    /// <summary>
    /// Map command to HTTP POST endpoint for being executed by a functional service.
    /// </summary>
    /// <param name="builder">Endpoint route builder instance</param>
    /// <param name="route">HTTP API route</param>
    /// <param name="enrichCommand">A function to populate command props from HttpContext</param>
    /// <param name="policyName">Authorization policy</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <typeparam name="TState">State type on which the command will operate</typeparam>
    /// <typeparam name="TResult">Result type that will be returned</typeparam>
    /// <returns></returns>
    public static RouteHandlerBuilder MapCommandFunc<TCommand, TState, TResult>(
            this IEndpointRouteBuilder              builder,
            string?                                 route,
            EnrichCommandFromHttpContext<TCommand>? enrichCommand = null,
            string?                                 policyName    = null
        )
        where TState : State<TState>
        where TCommand : class
        where TResult : Result<TState>
        => MapFunc<TState, TCommand, TCommand, TResult>(
            builder,
            route,
            enrichCommand != null
                ? (command, context) => enrichCommand(command, context)
                : (command, _) => command,
            policyName
        );

    static RouteHandlerBuilder MapFunc<TState, TContract, TCommand, TResult>(
            IEndpointRouteBuilder                         builder,
            string?                                       route,
            ConvertAndEnrichCommand<TContract, TCommand>? convert    = null,
            string?                                       policyName = null
        )
        where TState : State<TState>
        where TCommand : class
        where TContract : class
        where TResult : Result<TState> {
        if (convert == null && typeof(TCommand) != typeof(TContract))
            throw new InvalidOperationException($"Command type {typeof(TCommand).Name} is not assignable from {typeof(TContract).Name}");

        var resolvedRoute = GetRoute<TContract>(route);
        ExtensionsEventSource.Log.HttpEndpointRegistered<TContract>(resolvedRoute);

        var routeBuilder = builder
            .MapPost(
                resolvedRoute,
                async Task<IResult> (HttpContext context, IFuncCommandService<TState> service) => {
                    var cmd = await context.Request.ReadFromJsonAsync<TContract>(context.RequestAborted);

                    if (cmd == null) throw new InvalidOperationException("Failed to deserialize the command");

                    var command = convert != null
                        ? convert(cmd, context)
                        : (cmd as TCommand)!;

                    var result = await InvokeService(service, command, context.RequestAborted);

                    return result.AsResult();
                }
            )
            .Accepts<TContract>()
            .ProducesOk(typeof(TResult))
            .ProducesProblemDetails(Status404NotFound)
            .ProducesProblemDetails(Status409Conflict)
            .ProducesProblemDetails(Status500InternalServerError)
            .ProducesValidationProblemDetails(Status400BadRequest);

        routeBuilder.AddPolicy(policyName);
        routeBuilder.AddAuthorization(typeof(TContract));

        return routeBuilder;
    }
}
