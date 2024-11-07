// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

/// <summary>
/// Base class for mapping message contracts
/// </summary>
public abstract class MessageMap<T, TContext> where T : MessageMap<T, TContext> {
    readonly TypeMap<Func<object, TContext, object>> _typeMap = new();

    /// <summary>
    /// Adds a mapping function between two types to the map
    /// </summary>
    /// <param name="map">Mapping function between two types</param>
    /// <typeparam name="TIn">Inbound type</typeparam>
    /// <typeparam name="TOut">Outbound type</typeparam>
    /// <returns></returns>
    public T Add<TIn, TOut>(Func<TIn, TContext, TOut> map) where TIn : class where TOut : class {
        _typeMap.Add<TIn>(Map);
        return (T)this;

        object Map(object inCmd, TContext context) => map((TIn)inCmd, context);
    }

    /// <summary>
    /// Add an enrichment function that will populate properties from the provided context.
    /// </summary>
    /// <param name="map">Enrichment function. It must return a new instance of the message</param>
    /// <typeparam name="TIn">Contract type</typeparam>
    /// <returns></returns>
    public T Add<TIn>(Func<TIn, TContext, TIn> map) where TIn : class {
        Add<TIn, TIn>(map);

        return (T)this;
    }

    /// <summary>
    /// Executes conversion on the inbound message using the previously registered mapping function.
    /// </summary>
    /// <param name="command">Inbound message</param>
    /// <param name="context">Context to be potentially applied to the transformation</param>
    /// <typeparam name="TIn">Inbound type</typeparam>
    /// <typeparam name="TOut">Outbound type</typeparam>
    /// <returns>Converted message</returns>
    /// <exception cref="Exceptions.MessageMappingException{TIn,TOut}">Thrown if there's no mapping function registered</exception>
    public TOut Convert<TIn, TOut>(TIn command, TContext context) where TIn : class {
        if (!_typeMap.TryGetValue<TIn>(out var mapper)) {
            throw new Exceptions.MessageMappingException<TIn, TOut>();
        }

        return (TOut)mapper(command, context);
    }
}

/// <summary>
/// Command map for transforming public (external) commands to domain (internal commands).
/// </summary>
public class CommandMap<TContext> : MessageMap<CommandMap<TContext>, TContext>;