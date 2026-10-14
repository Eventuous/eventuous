// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable CheckNamespace

using Eventuous.Diagnostics;
using Eventuous.Diagnostics.Tracing;

namespace Microsoft.Extensions.DependencyInjection;

[PublicAPI]
public static partial class ServiceCollectionExtensions {
    /// <param name="services"></param>
    extension(IServiceCollection services) {
        /// <summary>
        /// Registers a singleton event reader, preserving existing registrations.
        /// </summary>
        /// <typeparam name="T">Implementation of <see cref="IEventReader"/></typeparam>
        /// <returns></returns>
        public IServiceCollection AddEventReader<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : class, IEventReader {
            services.TryAddSingleton<T>();

            if (EventuousDiagnostics.Enabled) {
                services.TryAddSingleton(sp => TracedEventReader.Trace(sp.GetRequiredService<T>()));
            }
            else { services.TryAddSingleton<IEventReader>(sp => sp.GetRequiredService<T>()); }

            return services;
        }

        /// <summary>
        /// Registers a singleton event reader, preserving existing registrations.
        /// </summary>
        /// <param name="getService">Function to create an instance of <see cref="IEventReader"/></param>
        /// <typeparam name="T">Implementation of <see cref="IEventReader"/></typeparam>
        /// <returns></returns>
        public IServiceCollection AddEventReader<T>(Func<IServiceProvider, T> getService) where T : class, IEventReader {
            services.TryAddSingleton(getService);

            if (EventuousDiagnostics.Enabled) {
                services.TryAddSingleton(sp => TracedEventReader.Trace(sp.GetRequiredService<T>()));
            }
            else { services.TryAddSingleton<IEventReader>(sp => sp.GetRequiredService<T>()); }

            return services;
        }

        /// <summary>
        /// Registers a singleton event writer, preserving existing registrations.
        /// </summary>
        /// <typeparam name="T">Implementation of <see cref="IEventWriter"/></typeparam>
        /// <returns></returns>
        public IServiceCollection AddEventWriter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : class, IEventWriter {
            services.TryAddSingleton<T>();

            if (EventuousDiagnostics.Enabled) {
                services.TryAddSingleton(sp => TracedEventWriter.Trace(sp.GetRequiredService<T>()));
            }
            else { services.TryAddSingleton<IEventWriter>(sp => sp.GetRequiredService<T>()); }

            return services;
        }

        /// <summary>
        /// Registers a singleton event writer, preserving existing registrations.
        /// </summary>
        /// <param name="getService">Function to create an instance of <see cref="IEventWriter"/></param>
        /// <typeparam name="T">Implementation of <see cref="IEventWriter"/></typeparam>
        /// <returns></returns>
        public IServiceCollection AddEventWriter<T>(Func<IServiceProvider, T> getService) where T : class, IEventWriter {
            services.TryAddSingleton(getService);

            if (EventuousDiagnostics.Enabled) {
                services.TryAddSingleton(sp => TracedEventWriter.Trace(sp.GetRequiredService<T>()));
            }
            else { services.TryAddSingleton<IEventWriter>(sp => sp.GetRequiredService<T>()); }

            return services;
        }

        /// <summary>
        /// Registers a shared singleton event reader and writer, preserving existing registrations.
        /// </summary>
        /// <typeparam name="T">Implementation of <see cref="IEventWriter"/> and <see cref="IEventReader"/></typeparam>
        /// <returns></returns>
        public IServiceCollection AddEventReaderWriter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : class, IEventWriter, IEventReader {
            services.TryAddSingleton<T>();

            if (EventuousDiagnostics.Enabled) {
                services.TryAddSingleton(sp => TracedEventReader.Trace(sp.GetRequiredService<T>()));
                services.TryAddSingleton(sp => TracedEventWriter.Trace(sp.GetRequiredService<T>()));
            }
            else {
                services.TryAddSingleton<IEventReader>(sp => sp.GetRequiredService<T>());
                services.TryAddSingleton<IEventWriter>(sp => sp.GetRequiredService<T>());
            }

            return services;
        }

        /// <summary>
        /// Registers a shared singleton event reader and writer, preserving existing registrations.
        /// </summary>
        /// <param name="getService">Function to create an instance of the class,
        /// which implements both <see cref="IEventReader"/> and <see cref="IEventWriter"/></param>
        /// <typeparam name="T">Implementation of <see cref="IEventWriter"/></typeparam>
        /// <returns></returns>
        public IServiceCollection AddEventReaderWriter<T>(Func<IServiceProvider, T> getService)
            where T : class, IEventWriter, IEventReader {
            services.TryAddSingleton(getService);

            if (EventuousDiagnostics.Enabled) {
                services.TryAddSingleton(sp => TracedEventReader.Trace(sp.GetRequiredService<T>()));
                services.TryAddSingleton(sp => TracedEventWriter.Trace(sp.GetRequiredService<T>()));
            }
            else {
                services.TryAddSingleton<IEventWriter>(sp => sp.GetRequiredService<T>());
                services.TryAddSingleton<IEventReader>(sp => sp.GetRequiredService<T>());
            }

            return services;
        }

        /// <summary>
        /// Registers one singleton as reader, writer, and event store, preserving existing registrations.
        /// </summary>
        /// <typeparam name="T">Implementation of <see cref="IEventStore"/> </typeparam>
        /// <returns></returns>
        public IServiceCollection AddEventStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : class, IEventStore {
            services.TryAddSingleton<T>();

            if (EventuousDiagnostics.Enabled) {
                services.TryAddSingleton(sp => new TracedEventStore(sp.GetRequiredService<T>()));
                services.TryAddSingleton<IEventStore>(sp => sp.GetRequiredService<TracedEventStore>());
                services.TryAddSingleton<IEventReader>(sp => sp.GetRequiredService<TracedEventStore>());
                services.TryAddSingleton<IEventWriter>(sp => sp.GetRequiredService<TracedEventStore>());
            }
            else {
                services.TryAddSingleton<IEventReader>(sp => sp.GetRequiredService<T>());
                services.TryAddSingleton<IEventWriter>(sp => sp.GetRequiredService<T>());
                services.TryAddSingleton<IEventStore>(sp => sp.GetRequiredService<T>());
            }

            return services;
        }

        /// <summary>
        /// Registers one singleton as reader, writer, and event store, preserving existing registrations.
        /// </summary>
        /// <param name="getService">Function to create an instance of the class, which implements <see cref="IEventStore"/></param>
        /// <typeparam name="T">Implementation of <see cref="IEventStore"/></typeparam>
        /// <returns></returns>
        public IServiceCollection AddEventStore<T>(Func<IServiceProvider, T> getService) where T : class, IEventStore {
            services.TryAddSingleton(getService);

            if (EventuousDiagnostics.Enabled) {
                services.TryAddSingleton(sp => new TracedEventStore(sp.GetRequiredService<T>()));
                services.TryAddSingleton<IEventStore>(sp => sp.GetRequiredService<TracedEventStore>());
                services.TryAddSingleton<IEventReader>(sp => sp.GetRequiredService<TracedEventStore>());
                services.TryAddSingleton<IEventWriter>(sp => sp.GetRequiredService<TracedEventStore>());
            }
            else {
                services.TryAddSingleton<IEventWriter>(sp => sp.GetRequiredService<T>());
                services.TryAddSingleton<IEventReader>(sp => sp.GetRequiredService<T>());
                services.TryAddSingleton<IEventStore>(sp => sp.GetRequiredService<T>());
            }

            return services;
        }
    }
}
