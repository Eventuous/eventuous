// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.TestHelpers.Logging;

public record XUnitLoggerOptions {
    /// <summary>
    /// Includes scopes when <see langword="true" />.
    /// </summary>
    public bool IncludeScopes { get; set; }

    /// <summary>
    /// Includes category when <see langword="true" />.
    /// </summary>
    public bool IncludeCategory { get; set; } = true;

    /// <summary>
    /// Includes log level when <see langword="true" />.
    /// </summary>
    public bool IncludeLogLevel { get; set; } = true;

    /// <summary>
    /// Gets or sets format string used to format timestamp in logging messages. Defaults to <see langword="null" />.
    /// </summary>
    public string? TimestampFormat { get; set; }

    /// <summary>
    /// Gets or sets indication whether UTC timezone should be used to format timestamps in logging messages. Defaults to <see langword="false" />.
    /// </summary>
    public bool UseUtcTimestamp { get; set; } 
}
