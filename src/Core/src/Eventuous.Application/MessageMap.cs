// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public class MessageMap {
    readonly TypeMap<Func<object, object>> _typeMap = new();

    public MessageMap Add<TIn, TOut>(Func<TIn, TOut> map) where TIn : class where TOut : class {
        _typeMap.Add<TIn>(Map);
        return this;

        object Map(object inCmd) => map((TIn)inCmd);
    }

    public TOut Convert<TIn, TOut>(TIn command) where TIn : class {
        if (!_typeMap.TryGetValue<TIn>(out var mapper)) {
            throw new Exceptions.CommandMappingException<TIn, TOut>();
        }

        return (TOut)mapper(command);
    }
}
