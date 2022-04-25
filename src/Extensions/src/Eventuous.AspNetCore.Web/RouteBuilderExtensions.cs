using System.Reflection;
using Eventuous.AspNetCore.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

// ReSharper disable CheckNamespace

namespace Microsoft.AspNetCore.Routing;

public delegate TCommand EnrichCommandFromHttpContext<TCommand>(TCommand command, HttpContext httpContext);

public static class RouteBuilderExtensions {
    /// <summary>
    /// Allows to add an HTTP endpoint for controller-less apps
    /// </summary>
    /// <param name="builder">Endpoint route builder instance</param>
    /// <param name="enrichCommand">A function to populate command props from HttpContext</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <typeparam name="TAggregate">Aggregate type on which the command will operate</typeparam>
    /// <returns></returns>
    [PublicAPI]
    public static RouteHandlerBuilder MapCommand<TCommand, TAggregate>(
        this IEndpointRouteBuilder              builder,
        EnrichCommandFromHttpContext<TCommand>? enrichCommand = null
    ) where TAggregate : Aggregate where TCommand : class {
        var attr  = typeof(TCommand).GetAttribute<HttpCommandAttribute>();
        var route = GetRoute<TCommand>(attr?.Route);
        return builder.MapCommand<TCommand, TAggregate>(route, enrichCommand);
    }

    /// <summary>
    /// Allows to add an HTTP endpoint for controller-less apps
    /// </summary>
    /// <param name="builder">Endpoint route builder instance</param>
    /// <param name="route">HTTP API route</param>
    /// <param name="enrichCommand">A function to populate command props from HttpContext</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <typeparam name="TAggregate">Aggregate type on which the command will operate</typeparam>
    /// <returns></returns>
    [PublicAPI]
    public static RouteHandlerBuilder MapCommand<TCommand, TAggregate>(
        this IEndpointRouteBuilder              builder,
        string                                  route,
        EnrichCommandFromHttpContext<TCommand>? enrichCommand = null
    ) where TAggregate : Aggregate where TCommand : class
        => Map<TAggregate, TCommand>(builder, route, enrichCommand);

    /// <summary>
    /// Creates an instance of <see cref="ApplicationServiceRouteBuilder{T}"/> for a given aggregate type, so you
    /// can explicitly map commands to HTTP endpoints. 
    /// </summary>
    /// <param name="builder">Endpoint route builder instance</param>
    /// <typeparam name="TAggregate">Aggregate type</typeparam>
    /// <returns></returns>
    [PublicAPI]
    public static ApplicationServiceRouteBuilder<TAggregate> MapAggregateCommands<TAggregate>(
        this IEndpointRouteBuilder builder
    ) where TAggregate : Aggregate
        => new(builder);

    /// <summary>
    /// Maps all commands annotated by <seealso cref="HttpCommandAttribute"/> to HTTP endpoints to be handled
    /// by <seealso cref="IApplicationService{T}"/> where T is the aggregate type provided. Only use it if your
    /// application only handles commands for one aggregate type.
    /// </summary>
    /// <param name="builder">Endpoint route builder instance</param>
    /// <param name="assemblies">List of assemblies to scan</param>
    /// <typeparam name="TAggregate">Aggregate type</typeparam>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    [PublicAPI]
    public static IEndpointRouteBuilder MapDiscoveredCommands<TAggregate>(
        this   IEndpointRouteBuilder builder,
        params Assembly[]            assemblies
    ) where TAggregate : Aggregate {
        var assembliesToScan = assemblies.Length == 0 ? AppDomain.CurrentDomain.GetAssemblies() : assemblies;

        var attributeType = typeof(HttpCommandAttribute);

        foreach (var assembly in assembliesToScan) {
            MapAssemblyCommands(assembly);
        }

        void MapAssemblyCommands(Assembly assembly) {
            var decoratedTypes = assembly.DefinedTypes.Where(
                x => x.IsClass && x.CustomAttributes.Any(a => a.AttributeType == attributeType)
            );

            var method = typeof(RouteBuilderExtensions).GetMethod(
                nameof(Map),
                BindingFlags.Static | BindingFlags.NonPublic
            )!;

            foreach (var type in decoratedTypes) {
                var attr = type.GetAttribute<HttpCommandAttribute>()!;

                if (attr.AggregateType != null && attr.AggregateType != typeof(TAggregate))
                    throw new InvalidOperationException(
                        $"Command aggregate is {attr.AggregateType.Name} but expected to be {typeof(TAggregate).Name}"
                    );

                var genericMethod = method.MakeGenericMethod(typeof(TAggregate), type);
                genericMethod.Invoke(null, new object?[] { builder, attr.Route, null });
            }
        }

        return builder;
    }

    static RouteHandlerBuilder Map<TAggregate, TCommand>(
        IEndpointRouteBuilder                   builder,
        string?                                 route,
        EnrichCommandFromHttpContext<TCommand>? enrichCommand = null
    ) where TAggregate : Aggregate where TCommand : notnull
        => builder
            .MapPost(
                GetRoute<TCommand>(route),
                async Task<IResult>(HttpContext context, IApplicationService<TAggregate> service) => {
                    var cmd = await context.Request.ReadFromJsonAsync<TCommand>(context.RequestAborted);

                    if (cmd == null) throw new InvalidOperationException("Failed to deserialize the command");

                    if (enrichCommand != null) cmd = enrichCommand(cmd, context);

                    var result = await service.Handle(cmd, context.RequestAborted);
                    return result.AsResult();
                }
            )
            .Accepts<TCommand>(false, "application/json")
            .Produces<Result>()
            .Produces<ErrorResult>(StatusCodes.Status404NotFound)
            .Produces<ErrorResult>(StatusCodes.Status409Conflict)
            .Produces<ErrorResult>(StatusCodes.Status400BadRequest);

    /// <summary>
    /// Maps commands that are annotated either with <seealso cref="AggregateCommands"/> and/or
    /// <seealso cref="HttpCommandAttribute"/> in given assemblies. Will use assemblies of the current
    /// application domain if no assembly is specified explicitly.
    /// </summary>
    /// <param name="builder">Endpoint router builder instance</param>
    /// <param name="assemblies">List of assemblies to scan</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    [PublicAPI]
    public static IEndpointRouteBuilder MapDiscoveredCommands(
        this   IEndpointRouteBuilder builder,
        params Assembly[]            assemblies
    ) {
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
                var parentAttribute = type.DeclaringType?.GetAttribute<AggregateCommands>();
                if (parentAttribute == null) continue;

                LocalMap(parentAttribute.AggregateType, type, attr.Route);
            }
        }

        void LocalMap(Type aggregateType, Type type, string? route) {
            var appServiceBase = typeof(IApplicationService<>);
            var appServiceType = appServiceBase.MakeGenericType(aggregateType);

            builder
                .MapPost(
                    GetRoute(type, route),
                    async Task<IResult>(HttpContext context) => {
                        var cmd = await context.Request.ReadFromJsonAsync(type, context.RequestAborted);

                        if (cmd == null) throw new InvalidOperationException("Failed to deserialize the command");

                        if (context.RequestServices.GetRequiredService(appServiceType) is not IApplicationService
                            service) throw new InvalidOperationException("Unable to resolve the application service");

                        var result = await service.Handle(cmd, context.RequestAborted);

                        return result.AsResult();
                    }
                )
                .Accepts(type, false, "application/json")
                .Produces<Result>()
                .Produces<ErrorResult>(StatusCodes.Status404NotFound)
                .Produces<ErrorResult>(StatusCodes.Status409Conflict)
                .Produces<ErrorResult>(StatusCodes.Status400BadRequest);
        }
    }

    static string GetRoute<TCommand>(string? route) => GetRoute(typeof(TCommand), route);

    static string GetRoute(MemberInfo type, string? route) {
        return route ?? Generate();

        string Generate() {
            var gen = type.Name;
            return char.ToLowerInvariant(gen[0]) + gen[1..];
        }
    }
}
