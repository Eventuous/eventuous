// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Sut.Domain;

namespace Eventuous.Sut.AspNetCore;

public record BookingResult : Result {
    public new BookingState? State { get; init; }
}