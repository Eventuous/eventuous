// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions.Registrations;

class ParameterMap {
    readonly Dictionary<Type, Func<IServiceProvider, dynamic?>> _resolvers = new();

    public void Add<TService, TImplementation>() where TImplementation : class {
        dynamic? Resolver(IServiceProvider provider) => provider.GetService(typeof(TImplementation));

        _resolvers.Add(typeof(TService), Resolver);
    }

    public void Add<TService, TImplementation>(Func<IServiceProvider, TImplementation> resolver)
        where TImplementation : class
        => _resolvers.Add(typeof(TService), resolver);

    public bool TryGetResolver(Type parameterType, out Func<IServiceProvider, dynamic?>? resolver) 
        => _resolvers.TryGetValue(parameterType, out resolver);
}
