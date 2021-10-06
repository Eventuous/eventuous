// ReSharper disable CheckNamespace

using System.Reflection;
using Eventuous;
using Eventuous.Subscriptions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

public class DefaultSubscriptionBuilder<T, TOptions> : ISubscriptionBuilder<T, TOptions>
    where T : SubscriptionService<TOptions>
    where TOptions : SubscriptionOptions {
    public DefaultSubscriptionBuilder(IServiceCollection services, string subscriptionId) {
        SubscriptionId = subscriptionId;
        Services       = services;
    }

    public string             SubscriptionId { get; }
    public IServiceCollection Services       { get; }

    public T Resolve(IServiceProvider sp) {
        var constructors = typeof(T)
            .GetConstructors()
            .Select(
                x => (
                    Ctor: x,
                    Options: x.GetParameters()
                        .SingleOrDefault(y => y.ParameterType == typeof(TOptions))
                )
            ).Where(x => x.Options != null).ToArray();

        if (constructors.Length is 0 or > 1)
            throw new ArgumentOutOfRangeException(
                typeof(T).Name,
                $"Subscription type must have {(constructors.Length > 1 ? "only " : "")} one constructor with options argument"
            );

        var (ctor, optionsParameter) = constructors[0];

        IEnumerable<IEventHandler> handlers = sp.GetServices<ResolveHandler>()
            .Select(x => x(sp, SubscriptionId))
            .Where(x => x != null)
            .ToArray()!;

        var args = ctor.GetParameters().Select(CreateArg).ToArray();

        if (ctor.Invoke(args) is not T instance)
            throw new InvalidOperationException($"Unable to instantiate {typeof(T)}");

        return instance;

        object? CreateArg(ParameterInfo parameterInfo) {
            if (parameterInfo == optionsParameter) {
                var options = Ensure.NotNull(sp.GetService<IOptionsSnapshot<TOptions>>(), typeof(TOptions).Name);
                return options.Get(SubscriptionId);
            }

            // ReSharper disable once ConvertIfStatementToReturnStatement
            if (parameterInfo.ParameterType == typeof(IEnumerable<IEventHandler>)) return handlers;

            return sp.GetService(parameterInfo.ParameterType);
        }
    }
}

public delegate IEventHandler? ResolveHandler(IServiceProvider sp, string subscriptionId);