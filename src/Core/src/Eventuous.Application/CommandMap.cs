// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public class CommandMap {
    readonly TypeMap<Func<object, object>> _typeMap = new();

    public CommandMap Add<TIn, TOut>(Func<TIn, TOut> map) where TIn : class where TOut : class {
        _typeMap.Add<TIn>(Map);
        return this;

        object Map(object inCmd) => map((TIn)inCmd);
    }

    // public
}
