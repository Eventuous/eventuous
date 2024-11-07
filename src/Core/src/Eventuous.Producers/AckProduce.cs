// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Producers; 

public delegate ValueTask AcknowledgeProduce(ProducedMessage message);

public delegate ValueTask ReportFailedProduce(ProducedMessage message, string error, Exception? exception);