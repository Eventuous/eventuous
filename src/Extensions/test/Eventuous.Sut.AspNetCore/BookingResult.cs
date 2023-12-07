// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Sut.Domain;

namespace Eventuous.Sut.AspNetCore;

public record BookingResult(BookingState? State, bool Success, IEnumerable<Change>? Changes = null) : Result<BookingState>(State, Success, Changes);
