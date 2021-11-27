using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Eventuous.AspNetCore.Web; 

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
        var route = typeof(TCommand).Name;
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
                return result.AsActionResult<TAggregate>();
            }
        );
}