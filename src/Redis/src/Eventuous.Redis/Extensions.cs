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

    public static RedisValue ToRedisValue(this long position) {
        if (position == 0) return "0-0";
        return new RedisValue($"{position / 10}-{position % 10}");
    }
}
