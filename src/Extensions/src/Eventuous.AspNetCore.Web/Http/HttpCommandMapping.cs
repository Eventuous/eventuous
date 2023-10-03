// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using System.Reflection;
using Eventuous.AspNetCore.Web;
using Eventuous.AspNetCore.Web.Diagnostics;

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
    /// <typeparam name="TAggregate">Aggregate type on which the command will operate</typeparam>
    /// <typeparam name="TResult">Result type that will be returned</typeparam>
    /// <returns></returns>
    [PublicAPI]
    public static RouteHandlerBuilder MapCommand<TCommand, TAggregate, TResult>(
            this IEndpointRouteBuilder              builder,
            EnrichCommandFromHttpContext<TCommand>? enrichCommand = null
        ) 
        where TAggregate : Aggregate 
        where TCommand : class 
        where TResult : class, new() {
        var attr = typeof(TCommand).GetAttribute<HttpCommandAttribute>();

        return builder.MapCommand<TCommand, TAggregate, TResult>(attr?.Route, enrichCommand, attr?.PolicyName);
    }

    /// <summary>
    /// Map command to HTTP POST endpoint.
    /// </summary>
    /// <param name="builder">Endpoint route builder instance</param>
    /// <param name="route">HTTP API route</param>
    /// <param name="enrichCommand">A function to populate command props from HttpContext</param>
    /// <param name="policyName">Authorization policy</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <typeparam name="TAggregate">Aggregate type on which the command will operate</typeparam>
    /// <typeparam name="TResult">Result type that will be returned</typeparam>
    /// <returns></returns>
    [PublicAPI]
    public static RouteHandlerBuilder MapCommand<TCommand, TAggregate, TResult>(
            this IEndpointRouteBuilder              builder,
            string?                                 route,
            EnrichCommandFromHttpContext<TCommand>? enrichCommand = null,
            string?                                 policyName    = null
        ) 
        where TAggregate : Aggregate 
        where TCommand : class
        where TResult : class, new()
        => Map<TAggregate, TCommand, TCommand, TResult>(
            builder,
            route,
            enrichCommand != null
                ? (command, context) => enrichCommand(command, context)
                : (command, _) => command,
            policyName
        );

    /// <summary>
    /// Creates an instance of <see cref="CommandServiceRouteBuilder{T}"/> for a given aggregate type, so you
    /// can explicitly map commands to HTTP endpoints. 
    /// </summary>
    /// <param name="builder">Endpoint route builder instance</param>
    /// <typeparam name="TAggregate">Aggregate type</typeparam>
    /// <typeparam name="TResult">Result type that will be returned</typeparam>
    /// <returns></returns>
    [PublicAPI]
    public static CommandServiceRouteBuilder<TAggregate, TResult> MapAggregateCommands<TAggregate, TResult>(this IEndpointRouteBuilder builder)
        where TAggregate : Aggregate
        where TResult : class, new()
        => new(builder);

    /// <summary>
    /// Maps all commands annotated by <seealso cref="HttpCommandAttribute"/> to HTTP endpoints to be handled
    /// by <seealso cref="ICommandService{T}"/> where T is the aggregate type provided. Only use it if your
    /// application only handles commands for one aggregate type.
    /// </summary>
    /// <param name="builder">Endpoint route builder instance</param>
    /// <param name="assemblies">List of assemblies to scan</param>
    /// <typeparam name="TAggregate">Aggregate type</typeparam>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    [PublicAPI]
    public static IEndpointRouteBuilder MapDiscoveredCommands<TAggregate>(this IEndpointRouteBuilder builder, params Assembly[] assemblies)
        where TAggregate : Aggregate {
        var assembliesToScan = assemblies.Length == 0
            ? AppDomain.CurrentDomain.GetAssemblies()
            : assemblies;

        var attributeType = typeof(HttpCommandAttribute);

        foreach (var assembly in assembliesToScan) {
            MapAssemblyCommands(assembly);
        }

        return builder;

        void MapAssemblyCommands(Assembly assembly) {
            var decoratedTypes = assembly.DefinedTypes.Where(
                x => x.IsClass && x.CustomAttributes.Any(a => a.AttributeType == attributeType)
            );

            var method = typeof(RouteBuilderExtensions).GetMethod(nameof(Map), BindingFlags.Static | BindingFlags.NonPublic)!;

            foreach (var type in decoratedTypes) {
                var attr = type.GetAttribute<HttpCommandAttribute>()!;

                if (attr.AggregateType != null && attr.AggregateType != typeof(TAggregate))
                    throw new InvalidOperationException(
                        $"Command aggregate is {attr.AggregateType.Name} but expected to be {typeof(TAggregate).Name}"
                    );

                var genericMethod = method.MakeGenericMethod(typeof(TAggregate), type, type, attr.ResultType );
                genericMethod.Invoke(null, new object?[] { builder, attr.Route, null, attr.PolicyName });
            }
        }
    }

    static RouteHandlerBuilder Map<TAggregate, TContract, TCommand, TResult>(
            IEndpointRouteBuilder                         builder,
            string?                                       route,
            ConvertAndEnrichCommand<TContract, TCommand>? convert    = null,
            string?                                       policyName = null
        ) 
        where TAggregate : Aggregate 
        where TCommand : class 
        where TContract : class 
        where TResult : class, new() {
        if (convert == null && typeof(TCommand) != typeof(TContract))
            throw new InvalidOperationException($"Command type {typeof(TCommand).Name} is not assignable from {typeof(TContract).Name}");

        var resolvedRoute = GetRoute<TContract>(route);
        ExtensionsEventSource.Log.HttpEndpointRegistered<TContract>(resolvedRoute);

        var routeBuilder = builder
            .MapPost(
                resolvedRoute,
                async Task<IResult> (HttpContext context, ICommandService<TAggregate> service) => {
                    var cmd = await context.Request.ReadFromJsonAsync<TContract>(context.RequestAborted);

                    if (cmd == null) throw new InvalidOperationException("Failed to deserialize the command");

                    var command = convert != null
                        ? convert(cmd, context)
                        : (cmd as TCommand)!;

                    var result = await InvokeService(service, command, context.RequestAborted);

                    return result.AsResult<TResult>();
                }
            )
            .Accepts<TContract>(false, "application/json")
            .Produces<Eventuous.OkResult>()
            .Produces<ErrorResult>(StatusCodes.Status404NotFound)
            .Produces<ErrorResult>(StatusCodes.Status409Conflict)
            .Produces<ErrorResult>(StatusCodes.Status400BadRequest);

        routeBuilder.AddPolicy(policyName);
        routeBuilder.AddAuthorization(typeof(TContract));

        return routeBuilder;
    }

    /// <summary>
    /// Maps commands that are annotated either with <seealso cref="AggregateCommandsAttribute"/> and/or
    /// <seealso cref="HttpCommandAttribute"/> in given assemblies. Will use assemblies of the current
    /// application domain if no assembly is specified explicitly.
    /// </summary>
    /// <param name="builder">Endpoint router builder instance</param>
    /// <param name="assemblies">List of assemblies to scan</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    [PublicAPI]
    public static IEndpointRouteBuilder MapDiscoveredCommands(this IEndpointRouteBuilder builder, params Assembly[] assemblies) {
        var assembliesToScan = assemblies.Length == 0
            ? AppDomain.CurrentDomain.GetAssemblies()
            : assemblies;

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
                var parentAttribute = type.DeclaringType?.GetAttribute<AggregateCommandsAttribute>();

                if (parentAttribute == null) continue;

                LocalMap(parentAttribute.AggregateType, type, attr.Route, attr.PolicyName, attr.ResultType);
            }
        }

        void LocalMap(Type aggregateType, Type type, string? route, string? policyName, Type resultType) {
            var appServiceBase = typeof(ICommandService<>);
            var appServiceType = appServiceBase.MakeGenericType(aggregateType);

            var routeBuilder = builder
                .MapPost(
                    GetRoute(type, route),
                    async Task<IResult> (HttpContext context) => {
                        var cmd = await context.Request.ReadFromJsonAsync(type, context.RequestAborted);

                        if (cmd == null) throw new InvalidOperationException("Failed to deserialize the command");

                        if (context.RequestServices.GetRequiredService(appServiceType) is not ICommandService service)
                            throw new InvalidOperationException("Unable to resolve the application service");

                        var result = await InvokeService(service, cmd, context.RequestAborted);


                        // Don't know how to solve this part
                        var method = typeof(ResultExtensions).GetMethod(nameof(ResultExtensions.AsResult), BindingFlags.Static | BindingFlags.Public)!;
                        var genericMethod = method.MakeGenericMethod(resultType);
                        return genericMethod.Invoke(null, new object?[] { result }) as IResult;
                    }
                )
                .Accepts(type, false, "application/json")
                .Produces<Eventuous.OkResult>()
                .Produces<ErrorResult>(StatusCodes.Status404NotFound)
                .Produces<ErrorResult>(StatusCodes.Status409Conflict)
                .Produces<ErrorResult>(StatusCodes.Status400BadRequest);

            routeBuilder.AddPolicy(policyName);
            routeBuilder.AddAuthorization(type);
        }
    }

    static string GetRoute<TCommand>(string? route)
        => GetRoute(typeof(TCommand), route);

    static string GetRoute(MemberInfo type, string? route) {
        return route ?? Generate();

        string Generate() {
            var gen = type.Name;

            return char.ToLowerInvariant(gen[0]) + gen[1..];
        }
    }

    static void AddAuthorization(this RouteHandlerBuilder builder, Type contractType) {
        var authAttr = contractType.GetAttribute<AuthorizeAttribute>();
        if (authAttr != null) builder.RequireAuthorization(authAttr);
    }

    static void AddPolicy(this RouteHandlerBuilder builder, string? policyName) {
        if (policyName != null) builder.RequireAuthorization(policyName.Split(','));
    }

    static readonly ConcurrentDictionary<Type, Delegate> MethodCache = new();

    static Task<Result> InvokeService(ICommandService service, object cmd, CancellationToken cancellationToken) {
        var type = cmd.GetType();

        var handleDelegate = MethodCache.GetOrAdd(
            type,
            t => {
                var method        = service.GetType().GetMethod(nameof(ICommandService.Handle));
                var genericMethod = method!.MakeGenericMethod(t);
                var typeArgs      = new[] { t, typeof(CancellationToken), typeof(Task<Result>) };
                var funcType      = typeof(Func<,,>).MakeGenericType(typeArgs);
                return Delegate.CreateDelegate(funcType, service, genericMethod);
            }
        );

        return ((Task<Result>)handleDelegate.DynamicInvoke(cmd, cancellationToken)!)!;
    }
}
