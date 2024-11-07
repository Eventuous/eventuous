// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public class MetadataDeserializationException(Exception inner) : Exception("Failed to deserialize metadata", inner);
