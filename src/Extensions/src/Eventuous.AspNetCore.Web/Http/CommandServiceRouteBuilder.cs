// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Eventuous.AspNetCore.Web;

[PublicAPI]
public class CommandServiceRouteBuilder<T>(IEndpointRouteBuilder builder) where T : Aggregate {
    /// <summary>
    /// Maps the given command type to an HTTP endpoint. The command class can be annotated with
    /// the <seealso cref="HttpCommandAttribute"/> if you need a custom route.
    /// </summary>
    /// <param name="enrichCommand">A function to populate command props from HttpContext</param>
    /// <param name="configure">Additional route configuration</param>
    /// <typeparam name="TCommand">Command class</typeparam>
    /// <returns></returns>
    public CommandServiceRouteBuilder<T> MapCommand<TCommand>(
            EnrichCommandFromHttpContext<TCommand>? enrichCommand = null,
            Action<RouteHandlerBuilder>?            configure     = null
        ) where TCommand : class {
        if (configure == null) { builder.MapCommand<TCommand, T>(enrichCommand); }
        else { configure(builder.MapCommand<TCommand, T>(enrichCommand)); }

        return this;
    }

    /// <summary>
    /// Maps the given command type to an HTTP endpoint using the specified route.
    /// </summary>
    /// <param name="route">HTTP route for the command</param>
    /// <param name="enrichCommand">A function to populate command props from HttpContext</param>
    /// <param name="configure">Additional route configuration</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <returns></returns>
    public CommandServiceRouteBuilder<T> MapCommand<TCommand>(
            string                                  route,
            EnrichCommandFromHttpContext<TCommand>? enrichCommand = null,
            Action<RouteHandlerBuilder>?            configure     = null
        ) where TCommand : class {
        if (configure == null) { builder.MapCommand<TCommand, T>(route, enrichCommand); }
        else { configure(builder.MapCommand<TCommand, T>(route, enrichCommand)); }

        return this;
    }

    /// <summary>
    /// Maps the given command type to an HTTP endpoint using the specified route.
    /// Allows conversion between HTTP contract and command type.
    /// </summary>
    /// <param name="route"></param>
    /// <param name="enrichCommand"></param>
    /// <param name="configure">Additional route configuration</param>
    /// <typeparam name="TContract"></typeparam>
    /// <typeparam name="TCommand"></typeparam>
    /// <returns></returns>
    public CommandServiceRouteBuilder<T> MapCommand<TContract, TCommand>(
            string                                       route,
            ConvertAndEnrichCommand<TContract, TCommand> enrichCommand,
            Action<RouteHandlerBuilder>?                 configure = null
        ) where TCommand : class where TContract : class {
        if (configure == null) { builder.MapCommand<TContract, TCommand, T>(route, Ensure.NotNull(enrichCommand)); }
        else { configure(builder.MapCommand<TContract, TCommand, T>(route, Ensure.NotNull(enrichCommand))); }

        return this;
    }

    /// <summary>
    /// Maps the given command type to an HTTP endpoint using the route from the <see cref="HttpCommandAttribute"/> attribute.
    /// Allows conversion between HTTP contract and command type.
    /// </summary>
    /// <param name="enrichCommand"></param>
    /// <param name="configure">Additional route configuration</param>
    /// <typeparam name="TContract"></typeparam>
    /// <typeparam name="TCommand"></typeparam>
    /// <returns></returns>
    public CommandServiceRouteBuilder<T> MapCommand<TContract, TCommand>(
            ConvertAndEnrichCommand<TContract, TCommand> enrichCommand,
            Action<RouteHandlerBuilder>?                 configure = null
        )
        where TCommand : class where TContract : class {
        var attr = typeof(TContract).GetAttribute<HttpCommandAttribute>();
        AttributeCheck.EnsureCorrectAggregate<TContract, T>(attr);

        if (configure == null) { builder.MapCommand<TContract, TCommand, T>(attr?.Route, Ensure.NotNull(enrichCommand)); }
        else { configure(builder.MapCommand<TContract, TCommand, T>(attr?.Route, Ensure.NotNull(enrichCommand))); }

        return this;
    }
}
