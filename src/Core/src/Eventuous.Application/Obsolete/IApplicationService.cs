// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable UnusedTypeParameter

namespace Eventuous;

[Obsolete("Use ICommandService instead")]
public interface IApplicationService : ICommandService { }

[Obsolete("Use ICommandService instead")]
public interface IApplicationService<T> : ICommandService<T> where T : Aggregate { }

[Obsolete("Use ICommandService instead")]
public interface IApplicationService<T, TState, TId>
    : ICommandService<T, TState, TId>
    where T : Aggregate<TState>
    where TState : State<TState>, new()
    where TId : AggregateId { }
