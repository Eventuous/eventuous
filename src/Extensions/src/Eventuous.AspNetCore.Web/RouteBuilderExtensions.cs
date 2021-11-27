using System.Reflection;
using Eventuous.AspNetCore.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable CheckNamespace

namespace Microsoft.AspNetCore.Routing;

public static class RouteBuilderExtensions {
    /// <summary>
    /// Allows to add an HTTP endpoint for controller-less apps
    /// </summary>
    /// <param name="builder">Endpoint route builder instance</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <typeparam name="TAggregate">Aggregate type on which the command will operate</typeparam>
    /// <returns></returns>
    [PublicAPI]
    public static RouteHandlerBuilder MapCommand<TCommand, TAggregate>(this IEndpointRouteBuilder builder)
        where TAggregate : Aggregate where TCommand : class {
        var attr  = typeof(TCommand).GetAttribute<HttpCommandAttribute>();
        var route = GetRoute<TCommand>(attr?.Route);
        return builder.MapCommand<TCommand, TAggregate>(route);
    }

    /// <summary>
    /// Allows to add an HTTP endpoint for controller-less apps
    /// </summary>
    /// <param name="builder">Endpoint route builder instance</param>
    /// <param name="route">HTTP API route</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <typeparam name="TAggregate">Aggregate type on which the command will operate</typeparam>
    /// <returns></returns>
    [PublicAPI]
    public static RouteHandlerBuilder MapCommand<TCommand, TAggregate>(
        this IEndpointRouteBuilder builder,
        string                     route
    ) where TAggregate : Aggregate where TCommand : class
        => builder
            .MapPost(
                route,
                async (TCommand cmd, IApplicationService<TAggregate> service, CancellationToken cancellationToken) => {
                    var result = await service.Handle(cmd, cancellationToken);
                    return result.AsResult();
                }
            )
            .Produces<OkResult>()
            .Produces<ErrorResult>(StatusCodes.Status404NotFound)
            .Produces<ErrorResult>(StatusCodes.Status409Conflict)
            .Produces<ErrorResult>(StatusCodes.Status400BadRequest);

    [PublicAPI]
    public static ApplicationServiceRouteBuilder<T> MapAggregateCommands<T>(this IEndpointRouteBuilder builder)
        where T : Aggregate => new(builder);

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

            foreach (var type in decoratedTypes) {
                var attr = type.GetAttribute<HttpCommandAttribute>()!;

                if (attr.AggregateType != null && attr.AggregateType != typeof(TAggregate))
                    throw new InvalidOperationException(
                        $"Command aggregate is {attr.AggregateType.Name} but expected to be {typeof(TAggregate).Name}"
                    );

                Map(type, attr.Route);
            }
        }

        void Map(Type type, string? route) {
            builder
                .MapPost(
                    GetRoute(type, route),
                    async Task<IResult>(HttpContext context, IApplicationService<TAggregate> service) => {
                        var cmd = await context.Request.ReadFromJsonAsync(type, context.RequestAborted);

                        if (cmd == null)
                            throw new InvalidOperationException("Failed to deserialize the command");

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

        return builder;
    }

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

                Map(parentAttribute.AggregateType, type, attr.Route);
            }
        }

        void Map(Type aggregateType, Type type, string? route) {
            var appServiceBase = typeof(IApplicationService<>);
            var appServiceType = appServiceBase.MakeGenericType(aggregateType);

            builder
                .MapPost(
                    GetRoute(type, route),
                    async Task<IResult>(HttpContext context) => {
                        var cmd = await context.Request.ReadFromJsonAsync(type, context.RequestAborted);

                        if (cmd == null)
                            throw new InvalidOperationException("Failed to deserialize the command");

                        if (context.RequestServices.GetRequiredService(appServiceType) is not IApplicationService
                            service)
                            throw new InvalidOperationException("Unable to resolve the application service");

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

    static IResult AsResult(this Result result)
        => result is ErrorResult error
            ? error.Exception switch {
                OptimisticConcurrencyException => Results.Conflict(error),
                AggregateNotFoundException     => Results.NotFound(error),
                _                              => Results.BadRequest(error)
            } : Results.Ok(result);

    static T? GetAttribute<T>(this Type type) where T : class => Attribute.GetCustomAttribute(type, typeof(T)) as T;

    static string GetRoute<TCommand>(string? route) => GetRoute(typeof(TCommand), route);

    static string GetRoute(Type type, string? route) {
        return route ?? Generate();

        string Generate() {
            var gen = type.Name;
            return char.ToLowerInvariant(gen[0]) + gen[1..];
        }
    }
}

[PublicAPI]
public class ApplicationServiceRouteBuilder<T> where T : Aggregate {
    readonly IEndpointRouteBuilder _builder;

    public ApplicationServiceRouteBuilder(IEndpointRouteBuilder builder) => _builder = builder;

    public ApplicationServiceRouteBuilder<T> MapCommand<TCommand>() where TCommand : class {
        _builder.MapCommand<TCommand, T>();
        return this;
    }

    public ApplicationServiceRouteBuilder<T> MapCommand<TCommand>(string route) where TCommand : class {
        _builder.MapCommand<TCommand, T>(route);
        return this;
    }
}