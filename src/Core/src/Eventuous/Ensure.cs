namespace Eventuous; 

[PublicAPI]
public static class Ensure {
    /// <summary>
    /// Checks if the object is not null, otherwise throws
    /// </summary>
    /// <param name="value">Object to check for null value</param>
    /// <param name="name">Name of the object to be used in the exception message</param>
    /// <typeparam name="T">Object type</typeparam>
    /// <returns>Non-null object value</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static T NotNull<T>(T? value, string name) where T : class
        => value ?? throw new ArgumentNullException(name);

    /// <summary>
    /// Checks if the string is not null or empty, otherwise throws
    /// </summary>
    /// <param name="value">String value to check</param>
    /// <param name="name">Name of the parameter to be used in the exception message</param>
    /// <returns>Non-null and not empty string</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static string NotEmptyString(string? value, string name)
        => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentNullException(name);
        
    /// <summary>
    /// Throws a <see cref="DomainException"/> with a given message if the condition is not met
    /// </summary>
    /// <param name="condition">Condition to check</param>
    /// <param name="errorMessage">Message for the exception</param>
    /// <exception cref="DomainException"></exception>
    public static void IsTrue(bool condition, Func<string> errorMessage) {
        if (!condition) throw new DomainException(errorMessage());
    }

    /// <summary>
    /// Throws a custom exception if the condition is not met
    /// </summary>
    /// <param name="condition">Condition to check</param>
    /// <param name="getException"></param>
    /// <exception cref="Exception"></exception>
    public static void IsTrue(bool condition, Func<Exception> getException) {
        if (!condition) throw getException();
    }
}