using StackExchange.Redis;
namespace Eventuous.Redis.Extension;

public static class Extensions
{
    public static long ToLong(this RedisValue value) {
        var parts = ((string?)value)?.Split('-');
        return Convert.ToInt64(parts?[0]) * 10 + Convert.ToInt64(parts?[1]);
    }

    public static ulong ToULong(this RedisValue value) {
        var parts = ((string?)value)?.Split('-');
        return Convert.ToUInt64(parts?[0]) * 10 + Convert.ToUInt64(parts?[1]);
    }

    public static RedisValue ToRedisValue(this StreamReadPosition position) {
        if (position == StreamReadPosition.Start) return "0-0";
        return new RedisValue($"{position.Value / 10}-{position.Value % 10}");
    }
}
