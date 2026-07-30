// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.SignalR.Server;

/// <summary>
/// Options for producing events to a specific SignalR client.
/// </summary>
/// <param name="ConnectionId">The SignalR connection ID of the target client.</param>
public record SignalRProduceOptions(string ConnectionId);
