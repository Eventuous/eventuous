using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions.Logging;

public class SubscriptionLog {
    readonly string _subscriptionId;

    public Logging? Debug   { get; }
    public ILogger? Logger { get; }

    public SubscriptionLog(ILoggerFactory? factory, string subscriptionId) {
        Logger         = factory?.CreateLogger($"Subscription-{subscriptionId}");
        _subscriptionId = subscriptionId;

        Debug = Logger?.IsEnabled(LogLevel.Debug) == true
            ? LogDebug
            : null;
    }

    void LogDebug(string message, object[]? args) {
        Logger?.LogDebug($"[{_subscriptionId}] {message}", args);
    }
    
    public void Info(string message, params object[]? args) {
        Logger?.LogInformation($"[{_subscriptionId}] {message}", args);
    }
    
    public void Log(LogLevel logLevel, Exception exception, string message, params object[]? args) {
        Logger?.Log(logLevel, exception, $"[{_subscriptionId}] {message}", args);
    }
    
    public void Error(string message, params object[]? args) {
        Logger?.LogError($"[{_subscriptionId}] {message}", args);
    }
    
    public void Error(Exception exception, string message, params object[]? args) {
        Logger?.LogError(exception, $"[{_subscriptionId}] {message}", args);
    }
    
    public void Warn(string message, params object[]? args) {
        Logger?.LogWarning($"[{_subscriptionId}] {message}", args);
    }
    
    public void Warn(Exception? exception, string message, params object[]? args) {
        Logger?.LogWarning(exception, $"[{_subscriptionId}] {message}", args);
    }
}