using Microsoft.AspNetCore.Routing;

namespace Eventuous.AspNetCore.Web;

[PublicAPI]
public class ApplicationServiceRouteBuilder<T> where T : Aggregate {
    readonly IEndpointRouteBuilder _builder;

    public ApplicationServiceRouteBuilder(IEndpointRouteBuilder builder) => _builder = builder;

    /// <summary>
    /// Maps the given command type to an HTTP endpoint. The command class can be annotated with
    /// the <seealso cref="HttpCommandAttribute"/> if you need a custom route.
    /// </summary>
    /// <param name="enrichCommand">A function to populate command props from HttpContext</param>
    /// <typeparam name="TCommand">Command class</typeparam>
    /// <returns></returns>
    public ApplicationServiceRouteBuilder<T> MapCommand<TCommand>(
        EnrichCommandFromHttpContext<TCommand>? enrichCommand = null
    ) where TCommand : class {
        _builder.MapCommand<TCommand, T>(enrichCommand);
        return this;
    }

    /// <summary>
    /// Maps the given command type to an HTTP endpoint using the specified route.
    /// </summary>
    /// <param name="route">HTTP route for the command</param>
    /// <param name="enrichCommand">A function to populate command props from HttpContext</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <returns></returns>
    public ApplicationServiceRouteBuilder<T> MapCommand<TCommand>(
        string                                  route,
        EnrichCommandFromHttpContext<TCommand>? enrichCommand = null
    ) where TCommand : class {
        _builder.MapCommand<TCommand, T>(route, enrichCommand);
        return this;
    }
}
