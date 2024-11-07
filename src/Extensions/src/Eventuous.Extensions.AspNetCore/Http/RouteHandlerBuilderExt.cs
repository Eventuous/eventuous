// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Eventuous.Extensions.AspNetCore;

static class RouteHandlerBuilderExt {
    public static RouteHandlerBuilder ProducesValidationProblemDetails(this RouteHandlerBuilder builder, int statusCode)
        => builder.Produces<ValidationProblemDetails>(statusCode, ContentTypes.ProblemDetails);

    public static RouteHandlerBuilder ProducesProblemDetails(this RouteHandlerBuilder builder, int statusCode)
        => builder.Produces<ProblemDetails>(statusCode, ContentTypes.ProblemDetails);

    static RouteHandlerBuilder ProducesOk(this RouteHandlerBuilder builder, Type resultType)
        => builder.Produces(StatusCodes.Status200OK, resultType, ContentTypes.Json);

    public static RouteHandlerBuilder ProducesOk<TState>(this RouteHandlerBuilder builder) where TState : class, new() 
        => builder.ProducesOk(typeof(Result<TState>.Ok));

    static RouteHandlerBuilder Accepts(this RouteHandlerBuilder builder, Type commandType) => builder.Accepts(commandType, false, ContentTypes.Json);

    public static RouteHandlerBuilder Accepts<T>(this RouteHandlerBuilder builder) => builder.Accepts(typeof(T));
}
