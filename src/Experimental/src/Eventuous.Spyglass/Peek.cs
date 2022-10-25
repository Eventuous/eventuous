// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Reflection;

namespace Eventuous.Spyglass;

public class Peek {
    public Peek AddDomainAssembly(Assembly assembly) {
        DomainAssemblies.Add(assembly);
        return this;
    }

    public void Scan(Assembly assembly) {

    }

    List<Assembly> DomainAssemblies { get; } = new();
}

