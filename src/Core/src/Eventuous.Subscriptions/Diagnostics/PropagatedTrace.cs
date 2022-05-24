// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace Eventuous.Subscriptions.Diagnostics;

public record PropagatedTrace(ActivityTraceId TraceId, ActivitySpanId SpanId) {
    public PropagatedTrace(Activity activity) : this(activity.TraceId, activity.SpanId) { }
}
