// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions; 

[PublicAPI]
public record SubscriptionOptions {
    /// <summary>
    /// Subscription id is used to match event handlers with one subscription
    /// </summary>
    public string SubscriptionId { get; set; } = null!;
        
    /// <summary>
    /// Set to true if you want the subscription to fail and stop if anything goes wrong.
    /// </summary>
    public bool ThrowOnError { get; set; }
    
    /// <summary>
    /// Custom event serializer. If not assigned, the default serializer will be used.
    /// </summary>
    public IEventSerializer? EventSerializer { get; set; }
}