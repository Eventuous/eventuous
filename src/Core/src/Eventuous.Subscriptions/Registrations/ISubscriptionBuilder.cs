// ReSharper disable CheckNamespace

using Eventuous.Subscriptions;

namespace Microsoft.Extensions.DependencyInjection;

public interface ISubscriptionBuilder {
    /// <summary>
    /// Gets the id of the subscription configured by this builder
    /// </summary>
    string SubscriptionId { get; }

    /// <summary>
    /// Gets the application service collection
    /// </summary>
    IServiceCollection Services { get; }
}

/// <summary>
/// A builder for configuring named subscription instances
/// </summary>
public interface ISubscriptionBuilder<T, TOptions> : ISubscriptionBuilder
    where T : EventSubscription<TOptions>
    where TOptions : SubscriptionOptions {
    T Resolve(IServiceProvider sp);
}