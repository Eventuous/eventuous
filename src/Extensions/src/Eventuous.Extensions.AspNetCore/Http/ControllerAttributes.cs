// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Extensions.AspNetCore.Http;

/// <summary>
/// Indicates that the decorated action produces a 200 OK response
/// with a <see cref="Result{TState}.Ok"/> payload for the given state type.
/// </summary>
/// <typeparam name="TState">State type for the result payload.</typeparam>
public class ProducesResult<TState>() : ProducesResponseTypeAttribute(typeof(Result<TState>.Ok), 200) where TState : State<TState>, new();

/// <summary>
/// Indicates that the decorated action may produce a 409 Conflict response
/// with a <see cref="ProblemDetails"/> payload. It can happen when there's an optimistic
/// concurrency conflict.
/// </summary>
public class ProducesConflict() : ProducesResponseTypeAttribute(typeof(ProblemDetails), 409);

/// <summary>
/// Indicates that the decorated action may produce a Not Found error response
/// represented by <see cref="ProblemDetails"/>.
/// </summary>
public class ProducesNotFound() : ProducesResponseTypeAttribute(typeof(ProblemDetails), 404);

/// <summary>
/// Indicates that the decorated action may produce a 400 Bad Request response
/// with a <see cref="ValidationProblemDetails"/> payload describing domain validation errors.
/// </summary>
public class ProducesDomainError() : ProducesResponseTypeAttribute(typeof(ValidationProblemDetails), 400);
