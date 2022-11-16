using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Eventuous.Diagnostics;

public static class ActivityExtensions {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? GetParentTag(this Activity activity, string tag)
        => activity.Parent?.Tags.FirstOrDefault(x => x.Key == tag).Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Activity CopyParentTag(
        this Activity activity,
        string        tag,
        string?       parentTag = null
    ) {
        var value = activity.GetParentTag(parentTag ?? tag);
        if (value != null) activity.SetTag(tag, value);
        return activity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Activity SetOrCopyParentTag(
        this Activity activity,
        string        tag,
        string?       value,
        string?       parentTag = null
    ) {
        var val = value ?? activity.GetParentTag(parentTag ?? tag);
        if (val != null) activity.SetTag(tag, val);
        return activity;
    }

    public static TracingMeta GetTracingData(this Activity activity)
        => new(
            activity.TraceId.ToString(),
            activity.SpanId.ToString(),
            activity.ParentSpanId.ToString()
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Activity SetActivityStatus(this Activity activity, ActivityStatus status) {
        var (activityStatusCode, description, exception) = status;

        var statusCode = activityStatusCode switch {
            ActivityStatusCode.Error => "ERROR",
            ActivityStatusCode.Ok    => "OK",
            _                        => "UNSET"
        };

        activity.SetStatus(activityStatusCode, description);
        activity.SetTag(TelemetryTags.Otel.StatusCode, statusCode);
        activity.SetTag(TelemetryTags.Otel.StatusDescription, description);

        return !activity.IsAllDataRequested ? activity : activity.SetException(exception);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static Activity SetException(this Activity activity, Exception? exception) {
        if (exception == null) return activity;
        
        var tags = new ActivityTagsCollection(
            new KeyValuePair<string, object?>[] {
                new(TelemetryTags.Exception.Type, exception.GetType().Name),
                new(TelemetryTags.Exception.Message, $"{exception.Message} {exception.InnerException?.Message}"),
                new(TelemetryTags.Exception.Stacktrace, exception.StackTrace)
            }
        );

        foreach (var (key, value) in tags) {
            activity.SetTag(key, value);
        }

        return activity.AddEvent(
            new ActivityEvent(TelemetryTags.Exception.EventName, DateTimeOffset.Now, tags)
        );
    }
}