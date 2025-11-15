// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Eventuous.Extensions.AspNetCore.Http;

static class RouteHandlerBuilderExt {
    extension(RouteHandlerBuilder builder) {
        /// <summary>
        /// Configures the route to produce a <see cref="ValidationProblemDetails"/> payload
        /// with the specified HTTP status code and the <c>application/problem+json</c> content type.
        /// </summary>
        /// <param name="statusCode">The HTTP status code to declare for validation problem responses.</param>
        /// <returns>The same <see cref="RouteHandlerBuilder"/> instance for chaining.</returns>
        public RouteHandlerBuilder ProducesValidationProblemDetails(int statusCode)
            => builder.Produces<ValidationProblemDetails>(statusCode, ContentTypes.ProblemDetails);

        /// <summary>
        /// Configures the route to produce a <see cref="ProblemDetails"/> payload
        /// with the specified HTTP status code and the <c>application/problem+json</c> content type.
        /// </summary>
        /// <param name="statusCode">The HTTP status code to declare for problem responses.</param>
        /// <returns>The same <see cref="RouteHandlerBuilder"/> instance for chaining.</returns>
        public RouteHandlerBuilder ProducesProblemDetails(int statusCode)
            => builder.Produces<ProblemDetails>(statusCode, ContentTypes.ProblemDetails);

        RouteHandlerBuilder ProducesOk(Type resultType)
            => builder.Produces(StatusCodes.Status200OK, resultType, ContentTypes.Json);

        /// <summary>
        /// Declares a successful <c>200 OK</c> response for the route with a JSON body
        /// containing <see cref="Result{TState}.Ok"/> for the specified state type.
        /// </summary>
        /// <typeparam name="TState">The state type wrapped by the successful result.</typeparam>
        /// <returns>The same <see cref="RouteHandlerBuilder"/> instance for chaining.</returns>
        public RouteHandlerBuilder ProducesOk<TState>() where TState : class, new()
            => builder.ProducesOk(typeof(Result<TState>.Ok));

        RouteHandlerBuilder Accepts(Type commandType) => builder.Accepts(commandType, false, ContentTypes.Json);
        /// <summary>
        /// Declares that the route accepts a JSON request body of the specified type.
        /// </summary>
        /// <typeparam name="T">The request/command type accepted by the route.</typeparam>
        /// <returns>The same <see cref="RouteHandlerBuilder"/> instance for chaining.</returns>
        public RouteHandlerBuilder Accepts<T>() => builder.Accepts(typeof(T));
    }
}
