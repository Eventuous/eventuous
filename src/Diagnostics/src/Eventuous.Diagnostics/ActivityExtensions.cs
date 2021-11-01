using System.Diagnostics;

namespace Eventuous.Diagnostics; 

public static class ActivityExtensions {
    public static string? GetParentTag(this Activity activity, string tag)
        => activity.Parent?.Tags.FirstOrDefault(x => x.Key == tag).Value;

    public static Activity CopyParentTag(this Activity activity, string tag, string? parentTag = null) {
        var value = activity.GetParentTag(parentTag ?? tag);
        if (value != null) activity.SetTag(tag, value);
        return activity;
    }

    public static Activity SetOrCopyParentTag(this Activity activity, string tag, string? value, string? parentTag = null) {
        var val = value ?? activity.GetParentTag(parentTag ?? tag);
        if (val != null) activity.SetTag(tag, val);
        return activity;
    }
}