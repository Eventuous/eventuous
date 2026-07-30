// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Producers;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

using Hosting;
using Extensions;
using System.Diagnostics.CodeAnalysis;

[PublicAPI]
public static class RegistrationExtensions {
    /// <param name="services"></param>
    extension(IServiceCollection services) {
        [Obsolete("Use AddProducer instead")]
        public void AddEventProducer<T>(T producer) where T : class, IProducer {
            services.AddProducer(producer);
        }

        /// <summary>
        /// Register a producer in the DI container as IProducer using a pre-instantiated instance.
        /// </summary>
        /// <param name="producer">Producer instance</param>
        /// <typeparam name="T">Producer implementation type</typeparam>
        public void AddProducer<T>(T producer) where T : class, IProducer {
            services.TryAddSingleton(producer);
            services.TryAddSingleton<IProducer>(sp => sp.GetRequiredService<T>());

            if (producer is IHostedService service) {
                services.TryAddSingleton(service);
            }
        }

        [Obsolete("Use AddProducer instead")]
        public void AddEventProducer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] T>(Func<IServiceProvider, T> getProducer)
            where T : class, IProducer {
            services.AddProducer(getProducer);
        }

        /// <summary>
        /// Register a producer in the DI container as IProducer using a factory function.
        /// </summary>
        /// <param name="getProducer">Function to resolve the producer from the service provider</param>
        /// <typeparam name="T">Producer implementation type</typeparam>
        public void AddProducer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] T>(Func<IServiceProvider, T> getProducer)
            where T : class, IProducer {
            services.TryAddSingleton(getProducer);
            AddCommon<T>(services);
        }

        [Obsolete("Use AddProducer instead")]
        public void AddEventProducer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.Interfaces)] T>()
            where T : class, IProducer {
            services.AddProducer<T>();
        }

        /// <summary>
        /// Register a producer in the DI container as IProducer using the default constructor.
        /// </summary>
        /// <typeparam name="T">Producer implementation type</typeparam>
        public void AddProducer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.Interfaces)] T>()
            where T : class, IProducer {
            services.TryAddSingleton<T>();
            AddCommon<T>(services);
        }

        public void AddHostedServiceIfSupported<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] T>() where T : class {
            if (typeof(T).GetInterfaces().Contains(typeof(IHostedService))) {
                // ReSharper disable once ConvertToLocalFunction
                Func<IServiceProvider, T> factory    = sp => sp.GetRequiredService<T>();
                var                       descriptor = ServiceDescriptor.Describe(typeof(IHostedService), factory, ServiceLifetime.Singleton);
                services.TryAddEnumerable(descriptor);
            }
        }
    }

    static void AddCommon<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] T>(IServiceCollection services) where T : class, IProducer {
        services.TryAddSingleton<IProducer>(sp => sp.GetRequiredService<T>());
        services.AddHostedServiceIfSupported<T>();
    }
}
