// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;

namespace Eventuous.Persistence;

[StructLayout(LayoutKind.Auto)]
public record struct ProposedEvent(object Data, Metadata Metadata);

[StructLayout(LayoutKind.Auto)]
public record struct ProposedAppend(StreamName StreamName, ExpectedStreamVersion ExpectedVersion, ProposedEvent[] Events);

public delegate ProposedAppend AmendAppend<in T>(ProposedAppend originalEvent, T context);

delegate ProposedAppend AmendAppend(ProposedAppend originalEvent, object context);
