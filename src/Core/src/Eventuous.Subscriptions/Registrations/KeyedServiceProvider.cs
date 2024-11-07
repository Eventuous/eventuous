// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.DependencyInjection;

namespace Eventuous.Subscriptions.Registrations;

class KeyedServiceProvider : IServiceProvider {
    readonly string                _key;
    readonly IKeyedServiceProvider _provider;

    public KeyedServiceProvider(IServiceProvider provider, string key) {
        if (provider is not IKeyedServiceProvider keyedServiceProvider) {
            throw new ArgumentException("Provider must be a keyed provider", nameof(provider));
        }

        _key      = key;
        _provider = keyedServiceProvider;
    }

    public object? GetService(Type serviceType) => _provider.GetKeyedService(serviceType, _key) ?? _provider.GetService(serviceType);
}
