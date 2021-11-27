using Eventuous.AspNetCore.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

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
        var route = attr?.Route ?? typeof(TCommand).Name;
        return builder.MapCommand<TCommand, TAggregate>(char.ToLowerInvariant(route[0]) + route[1..]);
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
        => builder.MapPost(
            route,
            async (TCommand cmd, IApplicationService<TAggregate> service, CancellationToken cancellationToken) => {
                var result = await service.Handle(cmd, cancellationToken);
                return result.AsResult<TAggregate>();
            }
        );

    [PublicAPI]
    public static ApplicationServiceRouteBuilder<T> MapAggregateCommands<T>(this IEndpointRouteBuilder builder) 
        where T : Aggregate => new(builder);
    
    static IResult AsResult<T>(this Result result) where T : Aggregate
        => result is ErrorResult error
            ? error.Exception switch {
                OptimisticConcurrencyException<T> => Results.Conflict(error),
                AggregateNotFoundException<T>     => Results.NotFound(error),
                _                                 => Results.BadRequest(error)
            } : Results.Ok(result);
    
    static T? GetAttribute<T>(this Type type) where T : class => Attribute.GetCustomAttribute(type, typeof(T)) as T;
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