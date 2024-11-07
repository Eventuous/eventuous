// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Extensions.AspNetCore;

public class ProducesResult<TState>() : ProducesResponseTypeAttribute(typeof(Result<TState>.Ok), 200) where TState : State<TState>, new();

public class ProducesConflict() : ProducesResponseTypeAttribute(typeof(ProblemDetails), 409);

public class ProducesNotFound() : ProducesResponseTypeAttribute(typeof(ProblemDetails), 409);

public class ProducesDomainError() : ProducesResponseTypeAttribute(typeof(ValidationProblemDetails), 400);
