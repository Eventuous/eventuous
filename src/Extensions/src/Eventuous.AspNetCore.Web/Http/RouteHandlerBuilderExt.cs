// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Eventuous.AspNetCore.Web;

static class RouteHandlerBuilderExt {
    public static RouteHandlerBuilder ProducesValidationProblemDetails(this RouteHandlerBuilder builder, int statusCode)
        => builder.Produces<ValidationProblemDetails>(statusCode, ContentTypes.ProblemDetails);

    public static RouteHandlerBuilder ProducesProblemDetails(this RouteHandlerBuilder builder, int statusCode)
        => builder.Produces<ProblemDetails>(statusCode, ContentTypes.ProblemDetails);

    public static RouteHandlerBuilder ProducesOk(this RouteHandlerBuilder builder, Type resultType)
        => builder.Produces(StatusCodes.Status200OK, resultType, ContentTypes.Json);

    public static RouteHandlerBuilder ProducesOk<T>(this RouteHandlerBuilder builder) where T : Result
        => builder.ProducesOk(typeof(T));

    public static RouteHandlerBuilder Accepts(this RouteHandlerBuilder builder, Type commandType)
        => builder.Accepts(commandType, false, ContentTypes.Json);

    public static RouteHandlerBuilder Accepts<T>(this RouteHandlerBuilder builder)
        => builder.Accepts(typeof(T));
}
