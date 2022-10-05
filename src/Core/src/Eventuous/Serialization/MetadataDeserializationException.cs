// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public class MetadataDeserializationException : Exception {
    public MetadataDeserializationException(Exception inner) : base("Failed to deserialize metadata", inner) { }
}
