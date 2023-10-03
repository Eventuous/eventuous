// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.
namespace Eventuous.Projections.MongoDB; 

public static class Options<TOptions> where TOptions: new() {
    public static TOptions New(Action<TOptions>? configure)  {
        var options = new TOptions();
        configure?.Invoke(options);

        return options;
    }
    
    public static TOptions DefaultIfNotConfigured(Action<TOptions>? configure, Func<TOptions> factory)  {
        if (configure is null) return factory();
        
        var options = new TOptions();
        configure(options);

        return options;
    }
    
    public static TOptions? NullIfNotConfigured(Action<TOptions>? configure) {
        if (configure is null) return default;
        
        var options = new TOptions();
        configure(options);

        return options;
    }
}
