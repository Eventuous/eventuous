// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous; 

/// <summary>
/// Function to add additional information to the event before it's stored.
/// </summary>
public delegate StreamEvent AmendEvent(StreamEvent originalEvent);

public delegate StreamEvent AmendEvent<in T>(StreamEvent originalEvent, T context);