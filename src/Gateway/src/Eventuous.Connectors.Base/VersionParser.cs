namespace Eventuous.Connectors.Base; 

public static class VersionParser {
    public static MessageType Parse(string originalType) {
        var split = originalType.Split('.');
        return IsVersion(split[0]) 
            ? new MessageType(originalType, split[0], string.Join('.', split.Skip(1))) 
            : new MessageType(originalType, split[^1], string.Join('.', split.Take(split.Length - 1)));

        bool IsVersion(string test) => test.Length > 1 && (test[0] == 'V' || test[0] == 'v') && char.IsDigit(test[1]);
    }
}

public record MessageType(string Original, string Version, string Type);