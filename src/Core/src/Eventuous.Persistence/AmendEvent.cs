// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous; 

/// <summary>
/// Function to add additional information to the event before it's stored.
/// </summary>
public delegate NewStreamEvent AmendEvent(NewStreamEvent originalEvent);

public delegate NewStreamEvent AmendEvent<in T>(NewStreamEvent originalEvent, T context);