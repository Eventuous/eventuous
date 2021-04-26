using System;

namespace Eventuous {
    public static class Ensure {
        public static T NotNull<T>(T? value, string name) where T : class
            => value ?? throw new ArgumentNullException(name);

        public static string NotEmptyString(string? value, string name)
            => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentNullException(name);
    }
}