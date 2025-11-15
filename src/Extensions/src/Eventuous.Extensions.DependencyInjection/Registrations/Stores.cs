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
        /// Registers the event reader
        /// </summary>
        /// <typeparam name="T">Implementation of <see cref="IEventReader"/></typeparam>
        /// <returns></returns>
        public IServiceCollection AddEventReader<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : class, IEventReader {
            if (EventuousDiagnostics.Enabled) {
                services.TryAddSingleton<T>();
                services.TryAddSingleton(sp => TracedEventReader.Trace(sp.GetRequiredService<T>()));
            }
            else { services.TryAddSingleton<IEventReader, T>(); }

            return services;
        }

        /// <summary>
        /// Registers the event reader
        /// </summary>
        /// <param name="getService">Function to create an instance of <see cref="IEventReader"/></param>
        /// <typeparam name="T">Implementation of <see cref="IEventReader"/></typeparam>
        /// <returns></returns>
        public IServiceCollection AddEventReader<T>(Func<IServiceProvider, T> getService) where T : class, IEventReader {
            if (EventuousDiagnostics.Enabled) {
                services.TryAddSingleton(getService);
                services.TryAddSingleton(sp => TracedEventReader.Trace(sp.GetRequiredService<T>()));
            }
            else { services.TryAddSingleton<IEventReader>(getService); }

            return services;
        }

        /// <summary>
        /// Registers the event writer
        /// </summary>
        /// <typeparam name="T">Implementation of <see cref="IEventWriter"/></typeparam>
        /// <returns></returns>
        public IServiceCollection AddEventWriter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : class, IEventWriter {
            if (EventuousDiagnostics.Enabled) {
                services.TryAddSingleton<T>();
                services.TryAddSingleton(sp => TracedEventWriter.Trace(sp.GetRequiredService<T>()));
            }
            else { services.TryAddSingleton<IEventWriter, T>(); }

            return services;
        }

        /// <summary>
        /// Registers the event writer
        /// </summary>
        /// <param name="getService">Function to create an instance of <see cref="IEventWriter"/></param>
        /// <typeparam name="T">Implementation of <see cref="IEventWriter"/></typeparam>
        /// <returns></returns>
        public IServiceCollection AddEventWriter<T>(Func<IServiceProvider, T> getService) where T : class, IEventWriter {
            if (EventuousDiagnostics.Enabled) {
                services.TryAddSingleton(getService);
                services.TryAddSingleton(sp => TracedEventWriter.Trace(sp.GetRequiredService<T>()));
            }
            else { services.TryAddSingleton<IEventWriter>(getService); }

            return services;
        }

        /// <summary>
        /// Registers the event reader and writer
        /// </summary>
        /// <typeparam name="T">Implementation of <see cref="IEventWriter"/> and <see cref="IEventReader"/></typeparam>
        /// <returns></returns>
        public IServiceCollection AddEventReaderWriter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : class, IEventWriter, IEventReader {
            if (EventuousDiagnostics.Enabled) {
                services.TryAddSingleton<T>();
                services.TryAddSingleton(sp => TracedEventReader.Trace(sp.GetRequiredService<T>()));
                services.TryAddSingleton(sp => TracedEventWriter.Trace(sp.GetRequiredService<T>()));
            }
            else {
                services.TryAddSingleton<IEventReader, T>();
                services.TryAddSingleton<IEventWriter, T>();
            }

            return services;
        }

        /// <summary>
        /// Registers the event reader and writer implemented by one class
        /// </summary>
        /// <param name="getService">Function to create an instance of the class,
        /// which implements both <see cref="IEventReader"/> and <see cref="IEventWriter"/></param>
        /// <typeparam name="T">Implementation of <see cref="IEventWriter"/></typeparam>
        /// <returns></returns>
        public IServiceCollection AddEventReaderWriter<T>(Func<IServiceProvider, T> getService)
            where T : class, IEventWriter, IEventReader {
            if (EventuousDiagnostics.Enabled) {
                services.TryAddSingleton(getService);
                services.TryAddSingleton(sp => TracedEventReader.Trace(sp.GetRequiredService<T>()));
                services.TryAddSingleton(sp => TracedEventWriter.Trace(sp.GetRequiredService<T>()));
            }
            else {
                services.TryAddSingleton<IEventWriter>(getService);
                services.TryAddSingleton<IEventReader>(getService);
            }

            return services;
        }

        /// <summary>
        /// Registers an event store service as reader, writer, and event store
        /// </summary>
        /// <typeparam name="T">Implementation of <see cref="IEventStore"/> </typeparam>
        /// <returns></returns>
        public IServiceCollection AddEventStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : class, IEventStore {
            if (EventuousDiagnostics.Enabled) {
                services.TryAddSingleton<T>();
                services.AddSingleton(sp => new TracedEventStore(sp.GetRequiredService<T>()));
                services.AddSingleton<IEventStore>(sp => sp.GetRequiredService<TracedEventStore>());
                services.AddSingleton<IEventReader>(sp => sp.GetRequiredService<TracedEventStore>());
                services.AddSingleton<IEventWriter>(sp => sp.GetRequiredService<TracedEventStore>());
            }
            else {
                services.TryAddSingleton<IEventReader, T>();
                services.TryAddSingleton<IEventWriter, T>();
                services.TryAddSingleton<IEventStore, T>();
            }

            return services;
        }

        /// <summary>
        /// Registers an event store service as reader, writer, and event store
        /// </summary>
        /// <param name="getService">Function to create an instance of the class, which implements <see cref="IEventStore"/></param>
        /// <typeparam name="T">Implementation of <see cref="IEventStore"/></typeparam>
        /// <returns></returns>
        public IServiceCollection AddEventStore<T>(Func<IServiceProvider, T> getService) where T : class, IEventStore {
            if (EventuousDiagnostics.Enabled) {
                services.TryAddSingleton(getService);
                services.AddSingleton(sp => new TracedEventStore(sp.GetRequiredService<T>()));
                services.AddSingleton<IEventStore>(sp => sp.GetRequiredService<TracedEventStore>());
                services.AddSingleton<IEventReader>(sp => sp.GetRequiredService<TracedEventStore>());
                services.AddSingleton<IEventWriter>(sp => sp.GetRequiredService<TracedEventStore>());
            }
            else {
                services.TryAddSingleton<IEventWriter>(getService);
                services.TryAddSingleton<IEventReader>(getService);
                services.TryAddSingleton<IEventStore>(getService);
            }

            return services;
        }
    }
}
