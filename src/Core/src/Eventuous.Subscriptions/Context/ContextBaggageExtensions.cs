namespace Eventuous.Subscriptions.Context; 

public static class ContextBaggageExtensions {
    public static IMessageConsumeContext WithItem<T>(this IMessageConsumeContext ctx, string key, T item) {
        ctx.Items.AddItem(key, item);
        return ctx;
    }
}