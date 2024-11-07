// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Reflection;
using Eventuous.Extensions.AspNetCore;
using Eventuous.Extensions.AspNetCore.Diagnostics;
using static Microsoft.AspNetCore.Http.StatusCodes;

// ReSharper disable CheckNamespace

namespace Microsoft.AspNetCore.Routing;

using Authorization;
using Builder;
using Http;

public delegate TCommand EnrichCommandFromHttpContext<TCommand>(TCommand command, HttpContext httpContext);

public static partial class RouteBuilderExtensions {
    /// <summary>
    /// Map command to HTTP POST endpoint.
    /// The HTTP command type should be annotated with <seealso cref="HttpCommandAttribute"/> attribute.
    /// </summary>
    /// <param name="builder">Endpoint route builder instance</param>
    /// <param name="enrichCommand">A function to populate command props from HttpContext</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <typeparam name="TState">State type on which the command will operate</typeparam>
    /// <returns></returns>
    public static RouteHandlerBuilder MapCommand<TCommand, TState>(
            this IEndpointRouteBuilder              builder,
            EnrichCommandFromHttpContext<TCommand>? enrichCommand = null
        )
        where TState : State<TState>, new()
        where TCommand : class {
        var attr = typeof(TCommand).GetAttribute<HttpCommandAttribute>();

        return builder.MapCommand<TCommand, TState>(attr?.Route, enrichCommand, attr?.PolicyName);
    }

    /// <summary>
    /// Map command to HTTP POST endpoint.
    /// </summary>
    /// <param name="builder">Endpoint route builder instance</param>
    /// <param name="route">HTTP API route</param>
    /// <param name="enrichCommand">A function to populate command props from HttpContext</param>
    /// <param name="policyName">Authorization policy</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <typeparam name="TState">State type on which the command will operate</typeparam>
    /// <returns></returns>
    public static RouteHandlerBuilder MapCommand<TCommand, TState>(
            this IEndpointRouteBuilder              builder,
            string?                                 route,
            EnrichCommandFromHttpContext<TCommand>? enrichCommand = null,
            string?                                 policyName    = null
        )
        where TState : State<TState>, new()
        where TCommand : class
        => MapInternal<TState, TCommand, TCommand>(
            builder,
            route,
            enrichCommand != null ? (command, context) => enrichCommand(command, context) : null,
            policyName
        );

    /// <summary>
    /// Creates an instance of <see cref="CommandServiceRouteBuilder{TState}"/> for a given aggregate type, so you
    /// can explicitly map commands to HTTP endpoints. 
    /// </summary>
    /// <param name="builder">Endpoint route builder instance</param>
    /// <typeparam name="TState">State type</typeparam>
    /// <returns></returns>
    public static CommandServiceRouteBuilder<TState> MapCommands<TState>(this IEndpointRouteBuilder builder)
        where TState : State<TState>, new() => new(builder);

    /// <summary>
    /// Maps all commands annotated by <seealso cref="HttpCommandAttribute"/> to HTTP endpoints to be handled
    /// by <seealso cref="ICommandService{TState}"/> where <code>TState</code> is the state type provided.
    /// Only use it if your application only handles commands for one state type.
    /// </summary>
    /// <param name="builder">Endpoint route builder instance</param>
    /// <param name="assemblies">List of assemblies to scan</param>
    /// <typeparam name="TState">State type</typeparam>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static IEndpointRouteBuilder MapDiscoveredCommands<TState>(this IEndpointRouteBuilder builder, params Assembly[] assemblies)
        where TState : State<TState> {
        var assembliesToScan = assemblies.Length == 0 ? AppDomain.CurrentDomain.GetAssemblies() : assemblies;

        var attributeType = typeof(HttpCommandAttribute);

        foreach (var assembly in assembliesToScan) {
            MapAssemblyCommands(assembly);
        }

        return builder;

        void MapAssemblyCommands(Assembly assembly) {
            var decoratedTypes = assembly.DefinedTypes.Where(
                x => x.IsClass && x.CustomAttributes.Any(a => a.AttributeType == attributeType)
            );

            foreach (var type in decoratedTypes) {
                var attr = type.GetAttribute<HttpCommandAttribute>()!;

                if (attr.StateType != null && attr.StateType != typeof(TState)) {
                    throw new InvalidOperationException($"Command state is {attr.StateType.Name} but expected to be {typeof(TState).Name}");
                }

                builder.LocalMap(typeof(TState), type, attr.Route, attr.PolicyName);
            }
        }
    }

    /// <summary>
    /// Maps commands that are annotated either with <seealso cref="HttpCommandsAttribute"/> and/or
    /// <seealso cref="HttpCommandAttribute"/> in given assemblies. Will use assemblies of the current
    /// application domain if no assembly is specified explicitly.
    /// </summary>
    /// <param name="builder">Endpoint router builder instance</param>
    /// <param name="assemblies">List of assemblies to scan</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    [PublicAPI]
    public static IEndpointRouteBuilder MapDiscoveredCommands(this IEndpointRouteBuilder builder, params Assembly[] assemblies) {
        var assembliesToScan = assemblies.Length == 0 ? AppDomain.CurrentDomain.GetAssemblies() : assemblies;

        var attributeType = typeof(HttpCommandAttribute);

        foreach (var assembly in assembliesToScan) {
            MapAssemblyCommands(assembly);
        }

        return builder;

        void MapAssemblyCommands(Assembly assembly) {
            var decoratedTypes = assembly.DefinedTypes.Where(
                x => x.IsClass && x.CustomAttributes.Any(a => a.AttributeType == attributeType)
            );

            foreach (var type in decoratedTypes) {
                var attr            = type.GetAttribute<HttpCommandAttribute>()!;
                var parentAttribute = type.DeclaringType?.GetAttribute<HttpCommandsAttribute>();

                var stateType = parentAttribute?.StateType ?? attr.StateType;

                if (stateType == null) continue;

                if (parentAttribute != null && stateType != parentAttribute.StateType) {
                    throw new InvalidOperationException(
                        $"Command state type {stateType.Name} doesn't match with parent state type {parentAttribute.StateType.Name}"
                    );
                }

                builder.LocalMap(stateType, type, attr.Route, attr.PolicyName);
            }
        }
    }

    static void LocalMap(this IEndpointRouteBuilder builder, Type stateType, Type type, string? route, string? policyName) {
        var genericMethod = MapMethod.MakeGenericMethod(stateType, type, type);
        genericMethod.Invoke(null, [builder, route, null, policyName]);
    }

    static readonly MethodInfo MapMethod = typeof(RouteBuilderExtensions).GetMethod(nameof(MapInternal), BindingFlags.Static | BindingFlags.NonPublic)!;

    static RouteHandlerBuilder MapInternal<TState, TContract, TCommand>(
            IEndpointRouteBuilder                         builder,
            string?                                       route,
            ConvertAndEnrichCommand<TContract, TCommand>? convert    = null,
            string?                                       policyName = null
        )
        where TState : State<TState>, new()
        where TCommand : class
        where TContract : class {
        if (convert == null && typeof(TCommand) != typeof(TContract))
            throw new InvalidOperationException($"Command type {typeof(TCommand).Name} is not assignable from {typeof(TContract).Name}");

        var resolvedRoute = GetRoute(route);
        ExtensionsEventSource.Log.HttpEndpointRegistered<TContract>(resolvedRoute);

        var routeBuilder = builder
            .MapPost(
                resolvedRoute,
                async Task<IResult> (HttpContext context, ICommandService<TState> service) => {
                    var cmd = await context.Request.ReadFromJsonAsync<TContract>(context.RequestAborted);

                    if (cmd == null) throw new InvalidOperationException("Failed to deserialize the command");

                    var command = convert != null ? convert(cmd, context) : (cmd as TCommand)!;

                    var result = await service.Handle(command, context.RequestAborted);

                    return result.AsResult();
                }
            )
            .Accepts<TContract>()
            .ProducesOk<TState>()
            .ProducesProblemDetails(Status404NotFound)
            .ProducesProblemDetails(Status409Conflict)
            .ProducesProblemDetails(Status500InternalServerError)
            .ProducesValidationProblemDetails(Status400BadRequest);

        // Add policy
        if (policyName != null) routeBuilder.RequireAuthorization(policyName.Split(','));

        // Add authorization
        var authAttr = typeof(TContract).GetAttribute<AuthorizeAttribute>();
        if (authAttr != null) routeBuilder.RequireAuthorization(authAttr);

        return routeBuilder;

        static string GetRoute(string? route) {
            return route ?? Generate();

            string Generate() {
                var gen = typeof(TCommand).Name;

                return $"{char.ToLowerInvariant(gen[0])}{gen[1..]}";
            }
        }
    }
}
