namespace Eventuous.Subscriptions.Logging; 

public delegate void Logging(string format, params object[] args);

public delegate void LoggingWithException(string format, params object[] args);
