// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.DependencyInjection;

namespace Eventuous.Spyglass;

public static class RegistrationExtensions {
    /// <summary>
    /// Kept for API compatibility. The Spyglass registry is populated automatically
    /// via a source-generated module initializer; no DI registration is required.
    /// </summary>
    [Obsolete("No longer required. The Spyglass registry is populated automatically.")]
    public static IServiceCollection AddEventuousSpyglass(this IServiceCollection services) => services;
}
