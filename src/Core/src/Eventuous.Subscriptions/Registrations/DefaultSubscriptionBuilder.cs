// ReSharper disable CheckNamespace

using System.Reflection;
using Eventuous;
using Eventuous.Subscriptions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

public class DefaultSubscriptionBuilder<T, TOptions> : ISubscriptionBuilder<T, TOptions>
    where T : EventSubscription<TOptions>
    where TOptions : SubscriptionOptions {
    public DefaultSubscriptionBuilder(IServiceCollection services, string subscriptionId) {
        SubscriptionId = subscriptionId;
        Services       = services;
    }

    public string             SubscriptionId { get; }
    public IServiceCollection Services       { get; }
    
    T? Resolved { get; set; }

    public T Resolve(IServiceProvider sp) {
        const string subscriptionIdParameterName = "subscriptionId";

        if (Resolved != null) return Resolved;
        
        var constructors = typeof(T).GetConstructors<TOptions>();

        switch (constructors.Length) {
            case > 1:
                throw new ArgumentOutOfRangeException(
                    typeof(T).Name,
                    "Subscription type must have only one constructor with options argument"
                );
            case 0:
                constructors = typeof(T).GetConstructors<string>(subscriptionIdParameterName);
                break;
        }

        if (constructors.Length == 0) {
            throw new ArgumentOutOfRangeException(
                typeof(T).Name,
                "Subscription type must have at least one constructor with options or subscription id argument"
            );
        }

        var (ctor, parameter) = constructors[0];

        IEnumerable<IEventHandler> handlers = sp.GetServices<ResolveHandler>()
            .Select(x => x(sp, SubscriptionId))
            .Where(x => x != null)
            .ToArray()!;

        var args = ctor.GetParameters().Select(CreateArg).ToArray();

        if (ctor.Invoke(args) is not T instance)
            throw new InvalidOperationException($"Unable to instantiate {typeof(T)}");

        Resolved = instance;
        return instance;

        object? CreateArg(ParameterInfo parameterInfo) {
            if (parameterInfo == parameter) {
                if (parameter.Name == subscriptionIdParameterName) {
                    return SubscriptionId;
                }

                var options = Ensure.NotNull(sp.GetService<IOptionsSnapshot<TOptions>>(), typeof(TOptions).Name);
                return options.Get(SubscriptionId);
            }

            // ReSharper disable once ConvertIfStatementToReturnStatement
            if (parameterInfo.ParameterType == typeof(IEnumerable<IEventHandler>)) return handlers;

            return sp.GetService(parameterInfo.ParameterType);
        }
    }
}

static class TypeExtensionsForRegistrations {
    public static (ConstructorInfo Ctor, ParameterInfo Param)[] GetConstructors<T>(this Type type, string? name = null)
        => type
            .GetConstructors()
            .Select(
                x => (
                    Ctor: x,
                    Options: x.GetParameters()
                        .SingleOrDefault(y => y.ParameterType == typeof(T) && (name == null || y.Name == name))
                )
            ).Where(x => x.Options != null).ToArray()!;
}

public delegate IEventHandler? ResolveHandler(IServiceProvider sp, string subscriptionId);