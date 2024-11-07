// Copyright (C) 2021-2022 Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.DependencyInjection;

namespace Eventuous.Spyglass;

public static class RegistrationExtensions {
    public static IServiceCollection AddEventuousSpyglass(this IServiceCollection services)
        => services.AddSingleton<InsidePeek>();
}
